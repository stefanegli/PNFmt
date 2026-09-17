// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System.IO;

using PNFmt.Tests.Snapshots;

using Xunit;

namespace PNFmt.Tests.Formatter.EditorConfig
{
    public sealed class DefaultEditorConfigDocumentTests
    {
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

        [Fact]
        public void Csharp_member_sorting_defaults_preserve_existing_preferences()
        {
            const string Existing = "[*.cs]\npnfmt_csharp_sort_members = false\n"
                + "pnfmt_csharp_member_order = method,property\n"
                + "pnfmt_csharp_member_accessibility_order = none\n"
                + "pnfmt_csharp_sort_members_by_name = false\n";
            var updated = DefaultEditorConfigDocument.Update(Existing);
            Assert.Contains("pnfmt_csharp_sort_members = false", updated);
            Assert.Contains("pnfmt_csharp_member_order = method,property", updated);
            Assert.Contains("pnfmt_csharp_member_accessibility_order = none", updated);
            Assert.Contains("pnfmt_csharp_sort_members_by_name = false", updated);
            Assert.Equal(updated, DefaultEditorConfigDocument.Update(updated));
        }

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
        public void Defaults_do_not_reuse_differently_cased_glob_headers()
        {
            const string Existing = "[*.EDITORCONFIG]\npnfmt_sort_entries = false\n";

            var updated = DefaultEditorConfigDocument.Update(Existing);

            Assert.StartsWith(Existing, updated);
            Assert.Contains(
                "[*.editorconfig]\n"
                + "pnfmt_enabled = true\n"
                + "pnfmt_format = true\n"
                + "pnfmt_formatter = ini\n"
                + "pnfmt_ini_merge_groups = false\n"
                + "pnfmt_ini_sort_groups = false\n"
                + "pnfmt_sort_entries = true\n",
                updated);
        }

        [Fact]
        public void Defaults_preserve_disabled_processing_and_layout_preferences()
        {
            const string Existing = "[*.csproj]\npnfmt_enabled = false\npnfmt_format = false\npnfmt_formatter = csproj\n";
            var updated = DefaultEditorConfigDocument.Update(Existing);
            Assert.Contains("pnfmt_enabled = false\npnfmt_format = false\npnfmt_formatter = csproj", updated);
            Assert.Equal(updated, DefaultEditorConfigDocument.Update(updated));
        }

        [Theory]
        [InlineData("None")]
        [InlineData("xml")]
        public void Defaults_preserve_existing_formatter_selections(string selection)
        {
            var existing = "[*.csproj]\npnfmt_formatter = " + selection + "\n";
            var updated = DefaultEditorConfigDocument.Update(existing);
            Assert.Contains("pnfmt_formatter = " + selection, updated);
            Assert.DoesNotContain("pnfmt_formatter = csproj", updated);
            Assert.Equal(updated, DefaultEditorConfigDocument.Update(updated));
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
                + "pnfmt_enabled = true\n"
                + "pnfmt_format = true\n"
                + "pnfmt_formatter = ini\n"
                + "pnfmt_ini_group_by_prefix = true\n"
                + "pnfmt_ini_merge_groups = false\n"
                + "pnfmt_ini_sort_groups = false\n"
                + "pnfmt_sort_entries = false\n"
                + "zulu = unchanged",
                updated);
            Assert.Contains(
                "[*.resx]\n"
                + "pnfmt_enabled = true\n"
                + "pnfmt_format = true\n"
                + "pnfmt_formatter = resx\n"
                + "pnfmt_resx_insert_documentation_comment = false\n"
                + "pnfmt_resx_insert_xsd_schema = false\n"
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
        public void Region_removal_defaults_to_false_and_preserves_existing_values()
        {
            Assert.Contains("pnfmt_csharp_remove_regions = false", DefaultEditorConfigDocument.Update(string.Empty));
            var configured = DefaultEditorConfigDocument.Update("[*.cs]\npnfmt_csharp_remove_regions = true\n");
            Assert.Contains("pnfmt_csharp_remove_regions = true", configured);
            Assert.DoesNotContain("pnfmt_csharp_remove_regions = false", configured);
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
