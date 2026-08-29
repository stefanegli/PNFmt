namespace PNFmt.Tests.Formatter.Resx
{
    using NFluent;

    using PNFmt;

    using PNFmt.Tests.Formatter.Resx.Fake;
    using PNFmt.Tests.Formatter.Resx.TestFoundation;

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;

    using Xunit;

    public class ResxSnapshotTests
    {
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
            var expectedFile = Path.Combine(fixtureRoot, $"{baseFileName}-expected.resx");
            using (var actualFile = TemporaryFile.Copy(sourceFile))
            {
                var formatter = new ResxDocumentFormatter((IResxFormatSettings)settings, new FakeLog());
                var originalCulture = Thread.CurrentThread.CurrentCulture;
                try
                {
                    Thread.CurrentThread.CurrentCulture = new CultureInfo(culture ?? "en-US");

                    // Act
                    formatter.Run(actualFile.Path);

                    // Assert
                    var actual = TextNormalization.NormalizeLineEndings(File.ReadAllText(actualFile.Path));
                    var expected = TextNormalization.NormalizeLineEndings(File.ReadAllText(expectedFile));
                    Check.WithCustomMessage(message).That(actual).Equals(expected);
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
            var registeredInputs = new ResxSnapshotData().Create()
                .Select(testCase => testCase.Item2)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

            Assert.Equal(fixtureInputs, registeredInputs);
        }

        internal class ResxSnapshotData : TheoryDataBase<string, string, string, IResxFormatSettings>
        {
            public override IEnumerable<(string, string, string, IResxFormatSettings)> Create()
            {
                var sortAndRemoveDocumentation = new FakeSettings
                {
                    SortEntries = true,
                    RemoveXsdSchema = false,
                    RemoveDocumentationComment = true
                };

                yield return ("Culture should not impact sorting", "InvariantCulture.resx", "et", sortAndRemoveDocumentation);
                yield return ("Additional xml comments are kept.", "AdditionalXmlComments.resx", null, sortAndRemoveDocumentation);
                yield return ("Comment is removed even if no sorting is required.", "AlreadySorted.resx", null, sortAndRemoveDocumentation);
                yield return ("Data and metadata nodes are grouped and sorted.", "Mixed.resx", null, sortAndRemoveDocumentation);
                yield return ("Entries are sorted alphabetically.", "Sort.resx", null, sortAndRemoveDocumentation);
                yield return ("File remains untouched if no modification is necessary.", "NoModificationNeeded.resx", null, sortAndRemoveDocumentation);
                // TODO xml comments should retain their original position
                yield return ("Invalid resx files are not touched.", "InvalidResx.resx", null, sortAndRemoveDocumentation);
                yield return ("Meta data is sorted too.", "MetaData.resx", null, sortAndRemoveDocumentation);
                yield return ("Plain xml files are not touched.", "Plain.xml", null, sortAndRemoveDocumentation);
                yield return ("Comment nodes are kept.", "WithResxComments.resx", null, sortAndRemoveDocumentation);

                yield return ("Entries are only sorted if 'sort' setting is active.", "DoNotSort.resx", null, new FakeSettings
                {
                    SortEntries = false,
                    RemoveDocumentationComment = true
                });

                yield return ("Documentation is only removed if 'doc' setting is active.", "KeepComments.resx", null, new FakeSettings
                {
                    SortEntries = true,
                    RemoveDocumentationComment = false
                });

                yield return ("No formatter option means no rewrite.", "DoNothing.resx", null, new FakeSettings
                {
                    SortEntries = false,
                    RemoveDocumentationComment = false
                });
            }
        }
    }
}
