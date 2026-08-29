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
                var log = new TestLog();

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

        private sealed class TemporaryFile : IDisposable
        {
            private TemporaryFile(string directoryPath, string path)
            {
                this.DirectoryPath = directoryPath;
                this.Path = path;
            }

            public string DirectoryPath { get; }

            public string Path { get; }

            public static TemporaryFile Create(string name, string contents)
            {
                var directory = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "PNFmtIniTests",
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                var path = System.IO.Path.Combine(directory, name);
                File.WriteAllText(path, contents);
                return new TemporaryFile(directory, path);
            }

            public void Dispose()
            {
                Directory.Delete(this.DirectoryPath, true);
            }
        }

        private sealed class TestLog : IFormatterLog
        {
            public void Write(Exception exception)
            {
            }

            public void WriteLine(string message)
            {
            }
        }
    }
}
