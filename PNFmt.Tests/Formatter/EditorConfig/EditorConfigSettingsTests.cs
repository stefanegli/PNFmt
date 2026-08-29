// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace PNFmt.Tests.Formatter.EditorConfig
{
    public sealed class EditorConfigSettingsTests
    {
        [Fact]
        public void Csproj_reads_pnfmt_settings_and_standard_layout_settings()
        {
            const string Configuration =
                "root = true\n\n"
                + "[*.csproj]\n"
                + "pnfmt_sort_entries = true\n"
                + "pnfmt_empty_lines_between_groups = 0\n"
                + "pnfmt_sort_item_types = Protobuf, PackageReference, protobuf\n"
                + "indent_style = tab\n"
                + "tab_width = 8\n"
                + "end_of_line = lf\n";

            using (var target = TemporaryTarget.Create("Project.csproj", Configuration))
            {
                var log = new RecordingLog();
                var settings = new CsProjEditorConfigSettings(target.Path, log);

                Assert.True(settings.IsActive);
                Assert.True(settings.SortEntries);
                Assert.Equal(0, settings.EmptyLinesBetweenGroups);
                Assert.Equal(new[] { "Protobuf", "PackageReference" }, settings.SortItemTypes);
                Assert.Equal('\t', settings.IndentStyle);
                Assert.Equal(8, settings.TabWidth);
                Assert.Equal("\n", settings.EndOfLine);
                Assert.Empty(log.Messages);
            }
        }

        [Fact]
        public void Csproj_uses_every_legacy_formatter_setting_as_a_fallback()
        {
            const string Configuration =
                "root = true\n\n"
                + "[*.csproj]\n"
                + "csproj_formatter_sort_entries = true\n"
                + "csproj_formatter_empty_lines_between_groups = 0\n"
                + "csproj_formatter_sort_item_types = Protobuf\n";

            using (var target = TemporaryTarget.Create("Project.csproj", Configuration))
            {
                var log = new RecordingLog();
                var settings = new CsProjEditorConfigSettings(target.Path, log);

                Assert.True(settings.SortEntries);
                Assert.Equal(0, settings.EmptyLinesBetweenGroups);
                Assert.Equal(new[] { "Protobuf" }, settings.SortItemTypes);
                Assert.Equal(3, log.Messages.Count);
                Assert.All(log.Messages, message => Assert.Contains("warning PNFMT001", message));
                Assert.Contains(log.Messages, message => message.Contains("pnfmt_sort_entries"));
                Assert.Contains(log.Messages, message => message.Contains("pnfmt_empty_lines_between_groups"));
                Assert.Contains(log.Messages, message => message.Contains("pnfmt_sort_item_types"));
            }
        }

        [Fact]
        public void Csproj_pnfmt_settings_win_when_legacy_settings_are_also_present()
        {
            const string Configuration =
                "root = true\n\n"
                + "[*.csproj]\n"
                + "pnfmt_sort_entries = false\n"
                + "csproj_formatter_sort_entries = true\n"
                + "pnfmt_empty_lines_between_groups = 0\n"
                + "csproj_formatter_empty_lines_between_groups = 3\n"
                + "pnfmt_sort_item_types = Compile\n"
                + "csproj_formatter_sort_item_types = None\n";

            using (var target = TemporaryTarget.Create("Project.csproj", Configuration))
            {
                var log = new RecordingLog();
                var settings = new CsProjEditorConfigSettings(target.Path, log);

                Assert.False(settings.SortEntries);
                Assert.Equal(0, settings.EmptyLinesBetweenGroups);
                Assert.Equal(new[] { "Compile" }, settings.SortItemTypes);
                Assert.Equal(3, log.Messages.Count);
                Assert.All(log.Messages, message => Assert.Contains("deprecated and ignored", message));
            }
        }

        [Fact]
        public void Resx_reads_every_pnfmt_setting()
        {
            const string Configuration =
                "root = true\n\n"
                + "[*.resx]\n"
                + "pnfmt_sort_entries = true\n"
                + "pnfmt_remove_xsd_schema = true\n"
                + "pnfmt_remove_documentation_comment = false\n"
                + "pnfmt_sort_comparer = OrdinalIgnoreCase\n";

            using (var target = TemporaryTarget.Create("Strings.resx", Configuration))
            {
                var log = new RecordingLog();
                var settings = new ResxEditorConfigSettings(log, target.Path);

                Assert.True(settings.IsActive);
                Assert.True(settings.SortEntries);
                Assert.True(settings.RemoveXsdSchema);
                Assert.False(settings.RemoveDocumentationComment);
                Assert.Same(StringComparer.OrdinalIgnoreCase, settings.Comparer);
                Assert.Empty(log.Messages);
            }
        }

        [Fact]
        public void Resx_uses_every_legacy_setting_as_a_fallback()
        {
            const string Configuration =
                "root = true\n\n"
                + "[*.resx]\n"
                + "resx_formatter_sort_entries = true\n"
                + "resx_formatter_remove_xsd_schema = true\n"
                + "resx_formatter_remove_documentation_comment = true\n"
                + "resx_formatter_sort_comparer = InvariantCultureIgnoreCase\n";

            using (var target = TemporaryTarget.Create("Strings.resx", Configuration))
            {
                var log = new RecordingLog();
                var settings = new ResxEditorConfigSettings(log, target.Path);

                Assert.True(settings.SortEntries);
                Assert.True(settings.RemoveXsdSchema);
                Assert.True(settings.RemoveDocumentationComment);
                Assert.Same(StringComparer.InvariantCultureIgnoreCase, settings.Comparer);
                Assert.Equal(4, log.Messages.Count);
                Assert.All(log.Messages, message => Assert.Contains("warning PNFMT001", message));
            }
        }

        [Fact]
        public void Resx_pnfmt_settings_win_when_legacy_settings_are_also_present()
        {
            const string Configuration =
                "root = true\n\n"
                + "[*.resx]\n"
                + "pnfmt_sort_entries = true\n"
                + "resx_formatter_sort_entries = false\n"
                + "pnfmt_remove_xsd_schema = false\n"
                + "resx_formatter_remove_xsd_schema = true\n"
                + "pnfmt_remove_documentation_comment = false\n"
                + "resx_formatter_remove_documentation_comment = true\n"
                + "pnfmt_sort_comparer = OrdinalIgnoreCase\n"
                + "resx_formatter_sort_comparer = InvariantCulture\n";

            using (var target = TemporaryTarget.Create("Strings.resx", Configuration))
            {
                var log = new RecordingLog();
                var settings = new ResxEditorConfigSettings(log, target.Path);

                Assert.True(settings.SortEntries);
                Assert.False(settings.RemoveXsdSchema);
                Assert.False(settings.RemoveDocumentationComment);
                Assert.Same(StringComparer.OrdinalIgnoreCase, settings.Comparer);
                Assert.Equal(4, log.Messages.Count);
                Assert.All(log.Messages, message => Assert.Contains("deprecated and ignored", message));
            }
        }

        private sealed class RecordingLog : IFormatterLog
        {
            public List<string> Messages { get; } = new List<string>();

            public void Write(Exception exception)
            {
            }

            public void WriteLine(string message)
            {
                this.Messages.Add(message);
            }
        }

        private sealed class TemporaryTarget : IDisposable
        {
            private TemporaryTarget(string directoryPath, string path)
            {
                this.DirectoryPath = directoryPath;
                this.Path = path;
            }

            public string DirectoryPath { get; }

            public string Path { get; }

            public static TemporaryTarget Create(string fileName, string editorConfig)
            {
                var directoryPath = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "PNFmtEditorConfigTests",
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directoryPath);
                File.WriteAllText(System.IO.Path.Combine(directoryPath, ".editorconfig"), editorConfig);
                var path = System.IO.Path.Combine(directoryPath, fileName);
                File.WriteAllText(path, string.Empty);
                return new TemporaryTarget(directoryPath, path);
            }

            public void Dispose()
            {
                Directory.Delete(this.DirectoryPath, true);
            }
        }
    }
}
