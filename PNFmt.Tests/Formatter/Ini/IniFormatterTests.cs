// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using Xunit;

namespace PNFmt.Tests.Formatter.Ini
{
    public sealed class IniFormatterTests
    {
        [Fact]
        public void Sorts_contiguous_properties_and_normalizes_spacing()
        {
            const string Input =
                "z=last\n"
                + "root=true\n"
                + "\n"
                + "[*]\n"
                + "tab_width=4\n"
                + "indent_style = space\n"
                + "# Language settings\n"
                + "dotnet_style_var_elsewhere=true:suggestion\n"
                + "csharp_style_var_elsewhere = false:suggestion\n"
                + "value_with_hash = text # this is part of the value\n\n";
            const string Expected =
                "root = true\n"
                + "z = last\n"
                + "\n"
                + "[*]\n"
                + "indent_style = space\n"
                + "tab_width = 4\n"
                + "# Language settings\n"
                + "csharp_style_var_elsewhere = false:suggestion\n"
                + "dotnet_style_var_elsewhere = true:suggestion\n"
                + "value_with_hash = text # this is part of the value\n";

            Assert.Equal(Expected, IniDocumentFormatter.Format(Input));
        }

        [Fact]
        public void Comments_unknown_lines_and_sections_are_sort_barriers()
        {
            const string Input =
                "[second]\n"
                + "z = 1\n"
                + "# Keep this group here\n"
                + "b=2\n"
                + "a=3\n"
                + "vendor directive\n"
                + "d=4\n"
                + "c=5\n"
                + "[first]\n"
                + "b=2\n"
                + "a=1\n";
            const string Expected =
                "[second]\n"
                + "z = 1\n"
                + "# Keep this group here\n"
                + "a = 3\n"
                + "b = 2\n"
                + "vendor directive\n"
                + "c = 5\n"
                + "d = 4\n"
                + "[first]\n"
                + "a = 1\n"
                + "b = 2\n";

            Assert.Equal(Expected, IniDocumentFormatter.Format(Input));
        }

        [Fact]
        public void Dry_run_detects_changes_and_a_second_run_is_unchanged()
        {
            using (var file = TemporaryFile.Create("settings.ini", "z=2\na=1\n"))
            {
                var formatter = new IniFormatter();
                var log = NullFormatterLog.Instance;

                var dryRun = formatter.Format(new FileFormatRequest(file.Path, false, false, log));

                Assert.Equal(FileFormatStatus.Updated, dryRun.Status);
                Assert.Equal("z=2\na=1\n", File.ReadAllText(file.Path));

                var update = formatter.Format(new FileFormatRequest(file.Path, true, false, log));
                var secondRun = formatter.Format(new FileFormatRequest(file.Path, true, false, log));

                Assert.Equal(FileFormatStatus.Updated, update.Status);
                Assert.Equal(FileFormatStatus.Unchanged, secondRun.Status);
                Assert.Equal("a = 1\nz = 2\n", File.ReadAllText(file.Path));
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("false")]
        [InlineData("invalid")]
        public void Explicit_true_setting_is_required(string settingValue)
        {
            using (var file = TemporaryFile.Create(
                "settings.ini",
                "z=2\na=1\n",
                settingValue))
            {
                var formatter = new IniFormatter();
                var result = formatter.Format(
                    new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance));

                Assert.Equal(FileFormatStatus.Skipped, result.Status);
                Assert.Equal("z=2\na=1\n", File.ReadAllText(file.Path));
            }
        }

        [Fact]
        public void Sorts_groups_without_sorting_entries_when_only_group_sorting_is_enabled()
        {
            const string Input =
                "root=z\n"
                + "mode=a\n"
                + "\n"
                + "[second]\n"
                + "z=2\n"
                + "a=1\n"
                + "\n"
                + "[first]\n"
                + "d=4\n"
                + "c=3\n";
            const string Expected =
                "root = z\n"
                + "mode = a\n"
                + "\n"
                + "[first]\n"
                + "d = 4\n"
                + "c = 3\n"
                + "\n"
                + "[second]\n"
                + "z = 2\n"
                + "a = 1\n";

            using (var file = TemporaryFile.Create(
                "settings.ini",
                Input,
                settingValue: null,
                groupSettingValue: "true"))
            {
                var formatter = new IniFormatter();
                var result = formatter.Format(
                    new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance));

                Assert.Equal(FileFormatStatus.Updated, result.Status);
                Assert.Equal(Expected, File.ReadAllText(file.Path));
            }
        }

        [Fact]
        public void Sorts_group_contents_with_their_section_header()
        {
            const string Input =
                "[zeta]\n"
                + "; Zeta comment\n"
                + "value=z\n"
                + "[alpha]\n"
                + "# Alpha comment\n"
                + "value=a\n";
            const string Expected =
                "[alpha]\n"
                + "# Alpha comment\n"
                + "value = a\n"
                + "[zeta]\n"
                + "; Zeta comment\n"
                + "value = z\n";

            Assert.Equal(
                Expected,
                IniDocumentFormatter.Format(Input, sortEntries: true, sortGroups: true));
        }

        [Fact]
        public void Merges_same_named_groups_before_sorting_their_entries()
        {
            const string Input =
                "root = true\n\n"
                + "[shared]\n"
                + "zulu = 3\n"
                + "[other]\n"
                + "value = other\n"
                + "[SHARED]\n"
                + "alpha = 1\n"
                + "middle = 2\n";
            const string Expected =
                "root = true\n\n"
                + "[shared]\n"
                + "alpha = 1\n"
                + "middle = 2\n"
                + "zulu = 3\n\n"
                + "[other]\n"
                + "value = other\n";

            Assert.Equal(
                Expected,
                IniDocumentFormatter.Format(
                    Input,
                    sortEntries: true,
                    mergeGroups: true));
        }

        [Fact]
        public void Prefix_grouping_activates_formatting_and_sorts_the_property_block()
        {
            const string Input =
                "visual_basic_style = unchanged\n"
                + "csharp_style_var = true\n"
                + "indent_style = space\n"
                + "csharp_indent_braces = false\n";
            const string Expected =
                "csharp_indent_braces = false\n"
                + "csharp_style_var = true\n"
                + "\n"
                + "indent_style = space\n"
                + "visual_basic_style = unchanged\n";

            using (var file = TemporaryFile.Create(
                "settings.ini",
                Input,
                settingValue: null,
                prefixSettingValue: "true"))
            {
                var result = new IniFormatter().Format(
                    new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance));

                Assert.Equal(FileFormatStatus.Updated, result.Status);
                Assert.Equal(Expected, File.ReadAllText(file.Path));
            }
        }

        [Theory]
        [InlineData("false")]
        [InlineData("invalid")]
        public void Group_sorting_requires_true(string settingValue)
        {
            const string Input = "[second]\nz=2\n[first]\na=1\n";
            using (var file = TemporaryFile.Create(
                "settings.ini",
                Input,
                settingValue: null,
                groupSettingValue: settingValue))
            {
                var formatter = new IniFormatter();
                var result = formatter.Format(
                    new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance));

                Assert.Equal(FileFormatStatus.Skipped, result.Status);
                Assert.Equal(Input, File.ReadAllText(file.Path));
            }
        }

        [Theory]
        [InlineData("false")]
        [InlineData("invalid")]
        public void Group_merging_requires_true(string settingValue)
        {
            const string Input = "[shared]\nz=2\n[shared]\na=1\n";
            using (var file = TemporaryFile.Create(
                "settings.ini",
                Input,
                settingValue: null,
                mergeSettingValue: settingValue))
            {
                var result = new IniFormatter().Format(
                    new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance));

                Assert.Equal(FileFormatStatus.Skipped, result.Status);
                Assert.Equal(Input, File.ReadAllText(file.Path));
            }
        }

        [Fact]
        public void Group_merging_activates_formatting()
        {
            const string Input = "[shared]\nz=2\n[shared]\na=1\n";
            using (var file = TemporaryFile.Create(
                "settings.ini",
                Input,
                settingValue: null,
                mergeSettingValue: "true"))
            {
                var result = new IniFormatter().Format(
                    new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance));

                Assert.Equal(FileFormatStatus.Updated, result.Status);
                Assert.Equal("[shared]\nz = 2\na = 1\n", File.ReadAllText(file.Path));
            }
        }

        private sealed class TemporaryFile : IDisposable
        {
            private readonly TestDirectory directory;

            private TemporaryFile(TestDirectory directory, string path)
            {
                this.directory = directory;
                this.Path = path;
            }

            public string DirectoryPath => this.directory.Path;

            public string Path { get; }

            public static TemporaryFile Create(
                string name,
                string contents,
                string settingValue = "true",
                string groupSettingValue = null,
                string prefixSettingValue = null,
                string mergeSettingValue = null)
            {
                var directory = new TestDirectory();
                if (settingValue is not null
                    || groupSettingValue is not null
                    || prefixSettingValue is not null
                    || mergeSettingValue is not null)
                {
                    var settings = "root = true\n\n[*.ini]\n";
                    if (settingValue is not null)
                    {
                        settings += "pnfmt_sort_entries = " + settingValue + "\n";
                    }

                    if (groupSettingValue is not null)
                    {
                        settings += "pnfmt_ini_sort_groups = " + groupSettingValue + "\n";
                    }

                    if (prefixSettingValue is not null)
                    {
                        settings += "pnfmt_ini_group_by_prefix = " + prefixSettingValue + "\n";
                    }

                    if (mergeSettingValue is not null)
                    {
                        settings += "pnfmt_ini_merge_groups = " + mergeSettingValue + "\n";
                    }

                    directory.Write(
                        ".editorconfig",
                        settings);
                }

                var path = directory.Write(name, contents);
                return new TemporaryFile(directory, path);
            }

            public void Dispose()
            {
                this.directory.Dispose();
            }
        }

    }
}
