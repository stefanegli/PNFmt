// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EditorConfig.Core;
using PNFmt.Tests.Snapshots;
using Xunit;

namespace PNFmt.Tests.Formatter.Ini
{
    public sealed class EditorConfigPrecedenceTests
    {
        public static IEnumerable<object[]> Cases => new[]
        {
            new object[]
            {
                "OverlappingGlobs", "[*.resx]\nindent_size=2\n[*]\nindent_size=4\n", "4",
            },
            new object[]
            {
                "MergeAtLastOccurrence", "[*.resx]\nindent_size=2\n[*]\nindent_size=4\n"
                    + "[*.resx]\npnfmt_sort_entries=true\n", "4",
            },
            new object[]
            {
                "MergeAtFirstOccurrence", "[*.resx]\npnfmt_sort_entries=true\n[*]\nindent_size=4\n"
                    + "[*.resx]\nindent_size=2\n", "2",
            },
            new object[]
            {
                "AdjacentSections", "[*.resx]\nindent_size=2\npnfmt_sort_entries=false\n\n"
                    + "[*.resx]\nPNFMT_SORT_ENTRIES=true\nINDENT_SIZE=4\n"
                    + "[*.resx]\nindent_style=space\n", "4",
            },
            new object[]
            {
                "CaseSensitiveGlobs", "[*.resx]\nindent_size=2\n[*.RESX]\nindent_size=4\n", "2",
            },
            new object[]
            {
                "DuplicateKeysAndUnset", "[*.resx]\nindent_size=2\npnfmt_sort_entries=false\n"
                    + "INDENT_SIZE=4\nPNFMT_SORT_ENTRIES=true\nindent_size=unset\n"
                    + "# Keep this comment\nindent_style=space\n"
                    + "[*.resx]\nindent_style=unset\n", "unset",
            },
            new object[]
            {
                "InterveningUnset", "[*.resx]\nindent_size=2\n[*]\nINDENT_SIZE=unset\n"
                    + "[*.resx]\npnfmt_sort_entries=true\n", "unset",
            },
        };

        [Theory]
        [MemberData(nameof(Cases))]
        public void Formatting_preserves_effective_properties(
            string caseName,
            string sections,
            string expectedIndentSize)
        {
            using (var configuration = new TemporaryConfiguration())
            {
                var input = "root=true\n\n" + sections;
                var targets = new[] { "Resources.resx", "Resources.RESX", "sub/Resources.resx", "code.cs" };
                var before = targets.Select(target => configuration.Parse(input, target)).ToArray();
                Assert.Equal(expectedIndentSize, before[0]["indent_size"]);

                // Exercise every combination, including prefix grouping without key sorting.
                for (var options = 0; options < 16; options++)
                {
                    string Format(string text) => IniDocumentFormatter.Format(
                        text,
                        sortEntries: (options & 1) != 0,
                        sortGroups: (options & 2) != 0,
                        groupByPrefix: (options & 4) != 0,
                        mergeGroups: (options & 8) != 0,
                        isEditorConfig: true);

                    var actual = Format(input);
                    Assert.Equal(actual, Format(actual));
                    for (var index = 0; index < targets.Length; index++)
                    {
                        Assert.Equal(
                            before[index].OrderBy(pair => pair.Key),
                            configuration.Parse(actual, targets[index]).OrderBy(pair => pair.Key));
                    }

                    if (options == 15)
                    {
                        GitSnapshot.Match(actual, typeof(EditorConfigPrecedenceTests), caseName + ".editorconfig");
                    }
                }
            }
        }

        [Theory]
        [InlineData(".editorconfig")]
        [InlineData("settings.editorconfig")]
        [InlineData("settings.EDITORCONFIG")]
        public void File_formatter_applies_EditorConfig_precedence_rules(string fileName)
        {
            const string Input = "root=true\n\n[*.{editorconfig,EDITORCONFIG}]\n"
                + "pnfmt_ini_sort_groups=true\npnfmt_ini_merge_groups=true\n"
                + "pnfmt_ini_group_by_prefix=true\npnfmt_sort_entries=true\n\n"
                + "[*.resx]\npnfmt_sort_entries=true\n[*]\nindent_size=4\n"
                + "[*.resx]\nindent_size=2\n[*.resx]\nINDENT_SIZE=unset\n";
            using (var configuration = new TemporaryConfiguration())
            {
                var before = configuration.Parse(Input, "Resources.resx");
                var path = Path.Combine(configuration.DirectoryPath, fileName);
                File.WriteAllText(path, Input);
                var formatter = new IniFormatter();
                var result = formatter.Format(new FileFormatRequest(path, true, false, configuration));
                var actual = File.ReadAllText(path);

                Assert.Equal(FileFormatStatus.Updated, result.Status);
                Assert.Equal(
                    before.OrderBy(pair => pair.Key),
                    configuration.Parse(actual, "Resources.resx").OrderBy(pair => pair.Key));
                Assert.Equal(2, actual.Split("[*.resx]").Length - 1);
                Assert.Equal(
                    FileFormatStatus.Unchanged,
                    formatter.Format(new FileFormatRequest(path, true, false, configuration)).Status);
            }
        }

        [Fact]
        public void Generated_defaults_preserve_section_order_and_existing_values()
        {
            const string Input = "root=true\n\n[*.resx]\npnfmt_sort_entries=true\n"
                + "[*]\nindent_size=4\n[*.resx]\nindent_size=2\n";
            using (var configuration = new TemporaryConfiguration())
            {
                var generated = DefaultEditorConfigDocument.Update(Input);
                var before = configuration.Parse(generated, "Resources.resx");
                foreach (var target in new[] { ".editorconfig", "settings.ini" })
                {
                    var settings = configuration.Parse(generated, target);
                    Assert.Equal("false", settings["pnfmt_ini_sort_groups"]);
                    Assert.Equal("false", settings["pnfmt_ini_merge_groups"]);
                    Assert.Equal("true", settings["pnfmt_sort_entries"]);
                }

                var path = Path.Combine(configuration.DirectoryPath, ".editorconfig");
                var result = new IniFormatter().Format(new FileFormatRequest(path, true, false, configuration));
                var actual = File.ReadAllText(path);
                Assert.Equal(FileFormatStatus.Updated, result.Status);
                Assert.Equal(2, actual.Split("[*.resx]").Length - 1);
                Assert.Equal(
                    before.OrderBy(pair => pair.Key),
                    configuration.Parse(actual, "Resources.resx").OrderBy(pair => pair.Key));
            }
        }

        private sealed class TemporaryConfiguration : IDisposable, IFormatterLog
        {
            public TemporaryConfiguration()
            {
                this.DirectoryPath = Path.Combine(Path.GetTempPath(), "PNFmtPrecedenceTests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(this.DirectoryPath);
            }

            public string DirectoryPath { get; }

            public IReadOnlyDictionary<string, string> Parse(string text, string target)
            {
                File.WriteAllText(Path.Combine(this.DirectoryPath, ".editorconfig"), text);
                // Bypass the shared file cache so each comparison parses the new contents.
                var parser = new EditorConfigParser(EditorConfigFile.Parse, null, null);
                return parser.Parse(Path.Combine(this.DirectoryPath, target)).Properties;
            }

            public void Dispose()
            {
                Directory.Delete(this.DirectoryPath, true);
            }

            public void Write(Exception exception)
            {
                throw new InvalidOperationException("Unexpected formatter error.", exception);
            }

            public void WriteLine(string message)
            {
            }
        }
    }
}
