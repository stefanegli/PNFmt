// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System.IO;
using PNFmt.Tests.Snapshots;
using Xunit;

namespace PNFmt.Tests.Formatter.EditorConfig
{
    public sealed class DefaultEditorConfigDocumentTests
    {
        [Fact]
        public void Default_configuration_matches_snapshot_and_is_idempotent()
        {
            var actual = DefaultEditorConfigDocument.Update(string.Empty);

            Assert.Equal(actual, DefaultEditorConfigDocument.Update(actual));
            GitSnapshot.Match(
                actual,
                typeof(DefaultEditorConfigDocumentTests),
                "Default.editorconfig");
        }

        [Fact]
        public void Existing_rules_around_the_managed_block_are_preserved()
        {
            const string Existing =
                "root = false\n\n[*]\ncharset = utf-8\n\n"
                + DefaultEditorConfigDocument.StartMarker
                + "\nold = value\n"
                + DefaultEditorConfigDocument.EndMarker
                + "\n\n[*.md]\ntrim_trailing_whitespace = true\n";

            var updated = DefaultEditorConfigDocument.Update(Existing);

            Assert.StartsWith("root = false\n\n" + DefaultEditorConfigDocument.StartMarker, updated);
            Assert.DoesNotContain("old = value", updated);
            Assert.EndsWith("[*.md]\ntrim_trailing_whitespace = true\n", updated);
            Assert.True(
                updated.IndexOf(DefaultEditorConfigDocument.EndMarker, System.StringComparison.Ordinal)
                < updated.IndexOf("[*]", System.StringComparison.Ordinal));
        }

        [Fact]
        public void Legacy_settings_are_migrated_in_their_existing_sections()
        {
            const string Existing =
                "root = true\n\n"
                + "[*.csproj]\n"
                + "csproj_formatter_sort_entries = false\n"
                + "csproj_formatter_empty_lines_between_groups: 2\n\n"
                + "[*.resx]\n"
                + "resx_formatter_sort_comparer = InvariantCulture\n";

            var updated = DefaultEditorConfigDocument.Update(
                Existing,
                migrateLegacySettings: true,
                removeLegacySettings: false);

            Assert.Contains(
                "[*.csproj]\n"
                + "csproj_formatter_sort_entries = false\n"
                + "pnfmt_sort_entries = false\n"
                + "csproj_formatter_empty_lines_between_groups: 2\n"
                + "pnfmt_csproj_empty_lines_between_groups = 2",
                updated);
            Assert.Contains(
                "[*.resx]\n"
                + "resx_formatter_sort_comparer = InvariantCulture\n"
                + "pnfmt_resx_sort_comparer = InvariantCulture",
                updated);
        }

        [Fact]
        public void Legacy_settings_can_be_removed_after_migration()
        {
            const string Existing =
                "[*.resx]\n"
                + "pnfmt_sort_entries = false\n"
                + "resx_formatter_sort_entries = true\n"
                + "resx_formatter_remove_xsd_schema = false\n";

            var updated = DefaultEditorConfigDocument.Update(
                Existing,
                migrateLegacySettings: true,
                removeLegacySettings: true);

            Assert.DoesNotContain("resx_formatter_", updated);
            Assert.Contains(
                "[*.resx]\n"
                + "pnfmt_sort_entries = false\n"
                + "pnfmt_resx_remove_xsd_schema = false",
                updated);
        }

        [Fact]
        public void Conflicting_legacy_settings_in_one_section_are_rejected()
        {
            const string Existing =
                "[*.{csproj,resx}]\n"
                + "csproj_formatter_sort_entries = true\n"
                + "resx_formatter_sort_entries = false\n";

            Assert.Throws<InvalidDataException>(
                () => DefaultEditorConfigDocument.Update(
                    Existing,
                    migrateLegacySettings: true));
        }

        [Theory]
        [InlineData("# <pnfmt-defaults>\n")]
        [InlineData("# </pnfmt-defaults>\n# <pnfmt-defaults>\n")]
        [InlineData("# <pnfmt-defaults>\n# <pnfmt-defaults>\n# </pnfmt-defaults>\n")]
        public void Invalid_managed_markers_are_rejected(string contents)
        {
            Assert.Throws<InvalidDataException>(
                () => DefaultEditorConfigDocument.Update(contents));
        }
    }
}
