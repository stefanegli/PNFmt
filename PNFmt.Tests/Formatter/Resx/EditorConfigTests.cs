namespace PNFmt.Tests.Formatter.Resx
{
    using System;
    using System.Globalization;
    using System.IO;

    using PNFmt.Tests.Snapshots;

    using Xunit;

    public sealed class ResxEditorConfigSnapshotTests
    {
        // Keep the existing snapshot identities, including the paired folder case.
        [Theory]
        [InlineData("Alternate_sort_method_can_be_configured", "sort", "Sort.resx", true, null)]
        [InlineData("Different_file_extensions_can_be_processed", "filetype", "Sort.abc", true, null)]
        [InlineData("EditorConfig_files_can_be_specified_per_folder", "config1", "Sort.resx", true, "config2")]
        [InlineData("Formatter_reports_inactive_if_EditorConfig_does_not_enable_it", "inactive", "Sort.resx", false, null)]
        [InlineData("Resx_comment_and_schema_are_inserted_if_necessary", "insertCommentAndSchema", "Sort.resx", true, null)]
        [InlineData("Resx_comment_is_inserted_if_necessary", "insertComment", "Sort.resx", true, null)]
        [InlineData("Xsd_schema_can_be_removed", "removeXsdSchema", "Schema.resx", true, null)]
        public void Editorconfig_fixtures_match_snapshots(string snapshotTestName, string fixture, string fileName, bool active, string additionalFixture)
        {
            using var directory = TestDirectory.CopyFrom(Path.Combine(AppContext.BaseDirectory, "Formatter", "Resx", "_editor"));
            var formatter = new ResxFormatter();
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("en-US");
                foreach (var folder in additionalFixture is null ? new[] { fixture } : new[] { fixture, additionalFixture })
                {
                    var path = directory.GetPath(Path.Combine(folder, fileName));
                    var result = formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance));
                    if (!active)
                    {
                        Assert.Equal(FileFormatStatus.Skipped, result.Status);
                    }

                    GitSnapshot.Match(File.ReadAllText(path), typeof(ResxEditorConfigSnapshotTests),
                        additionalFixture is null ? fileName : folder + "/" + fileName, snapshotTestName);
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }
    }
}
