namespace PNFmt.Tests.Formatter.Resx
{
    using PNFmt;

    using PNFmt.Tests.Formatter.Resx.Fake;
    using PNFmt.Tests.Formatter.Resx.TestFoundation;
    using PNFmt.Tests.Snapshots;

    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;

    using Xunit;
    using Xunit.Sdk;

    public class ResxSnapshotTests
    {
        public static System.Collections.Generic.IEnumerable<object[]> FileSnapshots =>
            FileSnapshotCaseSource.Create(GetFixtureRoot(), ".resx")
                .Select(testCase => new object[] { testCase.RelativePath, testCase.InputFile, testCase.CaseName });

        [Theory]
        [MemberData(nameof(FileSnapshots))]
        public void Formatter_matches_snapshot(string relativePath, string inputFile, string caseName)
        {
            var actual = FormatterSnapshotTestRunner.FormatAndAssertIdempotent(
                new ResxFormatter(), GetFixtureRoot(), relativePath, inputFile, caseName);
            GitSnapshot.Match(actual, typeof(ResxSnapshotTests), caseName);
        }

        private static string GetFixtureRoot() =>
            Path.Combine(AppContext.BaseDirectory, "Formatter", "Resx", "_files");

        [Theory]
        [ClassData(typeof(ResxSnapshotData))]
        public void Files_are_processed_correctly(string message, string fileName, string culture, object settings)
        {
            // Arrange
            var baseFileName = Path.GetFileNameWithoutExtension(fileName);
            var fixtureRoot = Path.Combine(
                System.AppContext.BaseDirectory,
                "Formatter",
                "Resx",
                "_files");
            var sourceFile = Path.Combine(fixtureRoot, fileName);
            using (var actualFile = TemporaryFile.Copy(sourceFile))
            {
                var formatter = new ResxDocumentFormatter((IResxFormatSettings)settings, NullFormatterLog.Instance);
                var originalCulture = Thread.CurrentThread.CurrentCulture;
                try
                {
                    Thread.CurrentThread.CurrentCulture = new CultureInfo(culture ?? "en-US");

                    var originalBytes = File.ReadAllBytes(actualFile.Path);
                    formatter.Run(actualFile.Path, writeChanges: false);
                    var previewChanged = formatter.IsFileChanged;
                    Assert.Equal(originalBytes, File.ReadAllBytes(actualFile.Path));

                    formatter.Run(actualFile.Path);
                    var formattedBytes = File.ReadAllBytes(actualFile.Path);
                    Assert.Equal(previewChanged, formatter.IsFileChanged);
                    Assert.Equal(!originalBytes.SequenceEqual(formattedBytes), formatter.IsFileChanged);

                    formatter.Run(actualFile.Path, writeChanges: false);
                    Assert.False(formatter.IsFileChanged);
                    Assert.Equal(formattedBytes, File.ReadAllBytes(actualFile.Path));
                    formatter.Run(actualFile.Path);
                    Assert.False(formatter.IsFileChanged);
                    Assert.Equal(formattedBytes, File.ReadAllBytes(actualFile.Path));

                    var actual = File.ReadAllText(actualFile.Path);
                    try
                    {
                        GitSnapshot.Match(actual, typeof(ResxSnapshotTests), $"{baseFileName}.resx");
                    }
                    catch (XunitException exception)
                    {
                        throw new XunitException($"{message}{Environment.NewLine}{exception.Message}");
                    }
                }
                finally
                {
                    Thread.CurrentThread.CurrentCulture = originalCulture;
                }
            }
        }

        [Fact]
        public void Every_legacy_snapshot_input_is_registered()
        {
            var fixtureRoot = Path.Combine(
                System.AppContext.BaseDirectory,
                "Formatter",
                "Resx",
                "_files");
            var fixtureInputs = Directory.GetFiles(fixtureRoot)
                .Where(path => path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .Where(path => !Path.GetFileNameWithoutExtension(path)
                    .EndsWith("-expected", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
            var registeredInputs = new ResxSnapshotData()
                .Select(testCase => (string)testCase[1])
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

            Assert.Equal(fixtureInputs, registeredInputs);
        }

        internal class ResxSnapshotData : TheoryData<string, string, string, object>
        {
            public ResxSnapshotData()
            {
                var sortAndRemoveDocumentation = new FakeSettings
                {
                    SortEntries = true,
                    RemoveXsdSchema = false,
                    RemoveDocumentationComment = true
                };

                this.Add("Culture should not impact sorting", "InvariantCulture.resx", "et", sortAndRemoveDocumentation);
                this.Add("Additional xml comments are kept.", "AdditionalXmlComments.resx", null, sortAndRemoveDocumentation);
                this.Add("Comment is removed even if no sorting is required.", "AlreadySorted.resx", null, sortAndRemoveDocumentation);
                this.Add("Data and metadata nodes are grouped and sorted.", "Mixed.resx", null, sortAndRemoveDocumentation);
                this.Add("Entries are sorted alphabetically.", "Sort.resx", null, sortAndRemoveDocumentation);
                this.Add("File remains untouched if no modification is necessary.", "NoModificationNeeded.resx", null, sortAndRemoveDocumentation);
                this.Add("Invalid resx files are not touched.", "InvalidResx.resx", null, sortAndRemoveDocumentation);
                this.Add("Meta data is sorted too.", "MetaData.resx", null, sortAndRemoveDocumentation);
                this.Add("Plain xml files are not touched.", "Plain.xml", null, sortAndRemoveDocumentation);
                this.Add("Comment nodes are kept.", "WithResxComments.resx", null, sortAndRemoveDocumentation);

                this.Add("Entries are only sorted if 'sort' setting is active.", "DoNotSort.resx", null, new FakeSettings
                {
                    SortEntries = false,
                    RemoveDocumentationComment = true
                });

                this.Add("Documentation is only removed if 'doc' setting is active.", "KeepComments.resx", null, new FakeSettings
                {
                    SortEntries = true,
                    RemoveDocumentationComment = false
                });

                this.Add("No formatter option means no rewrite.", "DoNothing.resx", null, new FakeSettings
                {
                    SortEntries = false,
                    RemoveDocumentationComment = false
                });
            }
        }
    }
}
