// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;

using Xunit;

namespace PNFmt.Tests.Formatter.Ini
{
    public sealed class IniFormatterTests
    {
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
                string mergeSettingValue = null)
            {
                var directory = new TestDirectory();
                if (settingValue is not null
                    || groupSettingValue is not null
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
