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

            Assert.StartsWith("root = false\n\n[*]\ncharset = utf-8", updated);
            Assert.DoesNotContain("old = value", updated);
            Assert.DoesNotContain(DefaultEditorConfigDocument.StartMarker, updated);
            Assert.DoesNotContain(DefaultEditorConfigDocument.EndMarker, updated);
            Assert.Contains("[*.md]\ntrim_trailing_whitespace = true", updated);
        }

        [Fact]
        public void Missing_defaults_use_existing_sections_and_sorted_insertion_points()
        {
            const string Existing =
                "root = false\n\n"
                + "[*.ini]\n"
                + "alpha = unchanged\n"
                + "pnfmt_ini_sort_groups = false\n"
                + "pnfmt_sort_entries = false\n"
                + "zulu = unchanged\n\n"
                + "[*.resx]\n"
                + "pnfmt_resx_sort_comparer = CurrentCulture\n";

            var updated = DefaultEditorConfigDocument.Update(Existing);

            Assert.Contains(
                "[*.ini]\n"
                + "alpha = unchanged\n"
                + "pnfmt_ini_group_by_prefix = true\n"
                + "pnfmt_ini_merge_groups = false\n"
                + "pnfmt_ini_sort_groups = false\n"
                + "pnfmt_sort_entries = false\n"
                + "zulu = unchanged",
                updated);
            Assert.Contains(
                "[*.resx]\n"
                + "pnfmt_resx_remove_documentation_comment = true\n"
                + "pnfmt_resx_remove_xsd_schema = true\n"
                + "pnfmt_resx_sort_comparer = CurrentCulture\n"
                + "pnfmt_sort_entries = true",
                updated);
            Assert.Equal(1, CountOccurrences(updated, "[*.ini]"));
            Assert.Equal(1, CountOccurrences(updated, "[*.resx]"));
            Assert.DoesNotContain("#", updated);
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

            Assert.Contains("csproj_formatter_sort_entries = false", updated);
            Assert.Contains("pnfmt_sort_entries = false", updated);
            Assert.Contains("csproj_formatter_empty_lines_between_groups: 2", updated);
            Assert.Contains("pnfmt_csproj_empty_lines_between_groups = 2", updated);
            Assert.Contains("resx_formatter_sort_comparer = InvariantCulture", updated);
            Assert.Contains("pnfmt_resx_sort_comparer = InvariantCulture", updated);
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
            Assert.Contains("pnfmt_sort_entries = false", updated);
            Assert.Contains("pnfmt_resx_remove_xsd_schema = false", updated);
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

        [Fact]
        public void Defaults_do_not_reuse_differently_cased_glob_headers()
        {
            const string Existing = "[*.EDITORCONFIG]\npnfmt_sort_entries = false\n";

            var updated = DefaultEditorConfigDocument.Update(Existing);

            Assert.StartsWith(Existing, updated);
            Assert.Contains(
                "[*.editorconfig]\n"
                + "pnfmt_ini_merge_groups = false\n"
                + "pnfmt_ini_sort_groups = false\n"
                + "pnfmt_sort_entries = true\n",
                updated);
        }

        private static int CountOccurrences(string text, string value)
        {
            var count = 0;
            var startIndex = 0;
            while ((startIndex = text.IndexOf(
                value,
                startIndex,
                System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                startIndex += value.Length;
            }

            return count;
        }
    }
}
