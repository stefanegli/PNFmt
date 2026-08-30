namespace PNFmt.Tests.Formatter.Resx
{
    using NFluent;

    using PNFmt;

    using PNFmt.Tests.Formatter.Resx.Fake;
    using PNFmt.Tests.Snapshots;

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;

    using Xunit;

    public class ResxEditorConfigSnapshotTests : IDisposable
    {
        private readonly List<string> temporaryFiles = new List<string>();

        [Fact]
        public void Alternate_sort_method_can_be_configured()
        {
            // Arrange
            var actualFile = this.PrepareFile("sort", "Sort");

            var formatter = new ResxFormatter();
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-Us");

            // Act
            formatter.Format(CreateRequest(actualFile));

            // Assert
            GitSnapshot.Match(
                File.ReadAllText(actualFile),
                typeof(ResxEditorConfigSnapshotTests),
                "Sort.resx");
        }

        [Fact]
        public void Different_file_extensions_can_be_processed()
        {
            // Arrange
            var actualFile = this.PrepareFile("filetype", "Sort", "abc");

            var formatter = new ResxFormatter();
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-Us");

            // Act
            formatter.Format(CreateRequest(actualFile));

            // Assert
            GitSnapshot.Match(
                File.ReadAllText(actualFile),
                typeof(ResxEditorConfigSnapshotTests),
                "Sort.abc");
        }

        [Fact]
        public void EditorConfig_files_can_be_specified_per_folder()
        {
            // Arrange
            var actualFile1 = this.PrepareFile("config1", "Sort");
            var actualFile2 = this.PrepareFile("config2", "Sort");

            var formatter = new ResxFormatter();
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-Us");

            // Act
            formatter.Format(CreateRequest(actualFile1));
            formatter.Format(CreateRequest(actualFile2));

            // Assert
            GitSnapshot.Match(
                File.ReadAllText(actualFile1),
                typeof(ResxEditorConfigSnapshotTests),
                "config1/Sort.resx");
            GitSnapshot.Match(
                File.ReadAllText(actualFile2),
                typeof(ResxEditorConfigSnapshotTests),
                "config2/Sort.resx");
        }

        [Fact]
        public void Resx_comment_and_schema_are_inserted_if_necessary()
        {
            // Arrange
            var actualFile = this.PrepareFile("insertCommentAndSchema", "Sort");

            var formatter = new ResxFormatter();
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-Us");

            // Act
            formatter.Format(CreateRequest(actualFile));

            // Assert
            GitSnapshot.Match(
                File.ReadAllText(actualFile),
                typeof(ResxEditorConfigSnapshotTests),
                "Sort.resx");
        }

        [Fact]
        public void Resx_comment_is_inserted_if_necessary()
        {
            // Arrange
            var actualFile = this.PrepareFile("insertComment", "Sort");

            var formatter = new ResxFormatter();
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-Us");

            // Act
            formatter.Format(CreateRequest(actualFile));

            // Assert
            GitSnapshot.Match(
                File.ReadAllText(actualFile),
                typeof(ResxEditorConfigSnapshotTests),
                "Sort.resx");
        }

        [Fact]
        public void Xsd_schema_can_be_removed()
        {
            // Arrange
            var actualFile = this.PrepareFile("removeXsdSchema", "Schema");

            var formatter = new ResxFormatter();

            // Act
            formatter.Format(CreateRequest(actualFile));

            // Assert
            GitSnapshot.Match(
                File.ReadAllText(actualFile),
                typeof(ResxEditorConfigSnapshotTests),
                "Schema.resx");
        }

        [Fact]
        public void Formatter_reports_inactive_if_EditorConfig_does_not_enable_it()
        {
            // Arrange
            var actualFile = this.PrepareFile("inactive", "Sort");
            var formatter = new ResxFormatter();

            // Act
            var result = formatter.Format(CreateRequest(actualFile));

            // Assert
            GitSnapshot.Match(
                File.ReadAllText(actualFile),
                typeof(ResxEditorConfigSnapshotTests),
                "Sort.resx");
            Check.WithCustomMessage("formatter should report inactive")
                .That(result.Status)
                .IsEqualTo(FileFormatStatus.Skipped);
        }

        [Fact]
        public void Dry_run_detects_changes_without_writing_the_file()
        {
            // Arrange
            var actualFile = this.PrepareFile("sort", "Sort");
            var formatter = new ResxFormatter();
            var original = File.ReadAllText(actualFile);

            // Act
            var result = formatter.Format(CreateRequest(actualFile, writeChanges: false));

            // Assert
            Check.WithCustomMessage("dry-run should not write changes to disk")
                .That(File.ReadAllText(actualFile))
                .Equals(original);
            Check.WithCustomMessage("dry-run should still detect pending updates")
                .That(result.Status)
                .IsEqualTo(FileFormatStatus.Updated);
        }

        public void Dispose()
        {
            foreach (var file in this.temporaryFiles)
            {
                File.Delete(file);
            }
        }

        private static FileFormatRequest CreateRequest(string filePath, bool writeChanges = true)
        {
            return new FileFormatRequest(filePath, writeChanges, false, new FakeLog());
        }

        private string PrepareFile(string fixtureName, string baseFileName, string extension = "resx")
        {
            var fixtureFolder = Path.Combine(
                AppContext.BaseDirectory,
                "Formatter",
                "Resx",
                "_editor",
                fixtureName);
            var sourceFile = Path.Combine(fixtureFolder, $"{baseFileName}.{extension}");
            var actualFile = Path.Combine(fixtureFolder, $"{baseFileName}-actual-{Guid.NewGuid():N}.{extension}");

            File.Copy(sourceFile, actualFile);
            this.temporaryFiles.Add(actualFile);
            return actualFile;
        }
    }
}
