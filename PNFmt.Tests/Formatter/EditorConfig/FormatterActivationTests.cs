// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace PNFmt.Tests.Formatter.EditorConfig
{
    public sealed class FormatterActivationTests
    {
        [Theory]
        [MemberData(nameof(FormatterContractTests.ActivationCases), MemberType = typeof(FormatterContractTests))]
        public void Enablement_and_selection_activate_each_formatter(string name, string fileName, string input)
        {
            using (var directory = new TestDirectory())
            {
                directory.Write(".editorconfig", "root = true\n[*]\npnfmt_enabled = true\npnfmt_formatter = " + name.ToUpperInvariant() + "\n");
                var path = directory.Write(fileName, input);
                var log = new RecordingLog();
                var formatter = FormatterCatalog.CreateDefault().Formatters.Single(item => item.Name == name);

                var result = formatter.Format(new FileFormatRequest(path, false, false, log));

                Assert.NotEqual(FileFormatStatus.Skipped, result.Status);
                Assert.Empty(result.Diagnostics);
                Assert.DoesNotContain(log.Messages, message => message.Contains("PNFMT004"));
                Assert.Equal(input, File.ReadAllText(path));
            }
        }

        [Theory]
        [MemberData(nameof(FormatterContractTests.ActivationCases), MemberType = typeof(FormatterContractTests))]
        public void None_overrides_legacy_activation_and_lint(string name, string fileName, string input)
        {
            using (var directory = new TestDirectory())
            {
                directory.Write(".editorconfig", "root = true\n[*]\npnfmt_enabled = true\npnfmt_formatter = nOnE\n"
                    + "pnfmt_sort_entries = true\npnfmt_csharp_format = true\npnfmt_xml_format = true\n"
                    + "pnfmt_xaml_format = true\nindent_size = 4\ncharset = utf-8-bom\n");
                var path = directory.Write(fileName, input);
                var original = File.ReadAllBytes(path);
                var log = new RecordingLog();
                var formatter = FormatterCatalog.CreateDefault().Formatters.Single(item => item.Name == name);

                foreach (var lint in new[] { false, true })
                {
                    var result = formatter.Format(new FileFormatRequest(path, true, lint, log));
                    Assert.Equal(FileFormatStatus.Skipped, result.Status);
                    Assert.Empty(result.Diagnostics);
                    Assert.Equal(original, File.ReadAllBytes(path));
                }

                Assert.Empty(log.Messages);
            }
        }

        [Theory]
        [MemberData(nameof(FormatterContractTests.ActivationCases), MemberType = typeof(FormatterContractTests))]
        public void Missing_selection_preserves_legacy_activation_with_one_warning(string name, string fileName, string input)
        {
            using (var directory = new TestDirectory())
            {
                var path = directory.Write(fileName, input);
                var formatter = FormatterCatalog.CreateDefault().Formatters.Single(item => item.Name == name);
                directory.Write(".editorconfig", "root = true\n");
                var inactiveLog = new RecordingLog();
                Assert.Equal(FileFormatStatus.Skipped,
                    formatter.Format(new FileFormatRequest(path, false, false, inactiveLog)).Status);
                Assert.Empty(inactiveLog.Messages);

                directory.Write(".editorconfig", "root = true\n[*]\npnfmt_sort_entries = true\n"
                    + "pnfmt_csharp_format = true\npnfmt_xml_format = true\npnfmt_xaml_format = true\n");
                var log = new RecordingLog();
                var result = formatter.Format(new FileFormatRequest(path, false, false, log));

                Assert.NotEqual(FileFormatStatus.Skipped, result.Status);
                var warning = Assert.Single(log.Messages, message => message.Contains("warning PNFMT004"));
                Assert.Contains(path, warning);
                Assert.Contains("pnfmt_formatter = " + name, warning);
                Assert.Contains("pnfmt_enabled = false", warning);
                Assert.Contains("future version", warning);
                Assert.Equal(input, File.ReadAllText(path));
            }
        }

        [Fact]
        public void Old_format_switch_controls_layout_without_disabling_other_behaviors()
        {
            using (var directory = new TestDirectory())
            {
                directory.Write(".editorconfig", "root = true\n[*]\npnfmt_enabled = true\npnfmt_formatter = csharp\n"
                    + "pnfmt_csharp_format = false\npnfmt_sort_entries = true\n");
                var path = directory.Write("Sample.cs", "using Z;\nusing A;\nclass C{void M(){}}\n");
                var log = new RecordingLog();

                var result = new CSharpFormatter().Format(new FileFormatRequest(path, true, false, log));

                Assert.Equal(FileFormatStatus.Updated, result.Status);
                Assert.StartsWith("using A;\nusing Z;", File.ReadAllText(path));
                Assert.Contains("class C{void M(){}}", File.ReadAllText(path));
                Assert.DoesNotContain(log.Messages, message => message.Contains("PNFMT004"));
            }
        }

        [Fact]
        public void Nested_selections_and_unset_follow_editorconfig_inheritance()
        {
            using (var directory = new TestDirectory())
            {
                directory.Write(".editorconfig", "root = true\n[*]\npnfmt_enabled = true\npnfmt_formatter = None\npnfmt_xml_format = true\n");
                directory.Write("nested/.editorconfig", "[*.xml]\npnfmt_formatter = XML\n"
                    + "[Legacy.xml]\npnfmt_formatter = unset\n");
                var disabled = directory.Write("Disabled.xml", "<root/>");
                var selected = directory.Write("nested/Selected.xml", "<root/>");
                var legacy = directory.Write("nested/Legacy.xml", "<root/>");
                var log = new RecordingLog();
                var formatter = new XmlFormatter();

                Assert.Equal(FileFormatStatus.Skipped, formatter.Format(new FileFormatRequest(disabled, false, false, log)).Status);
                Assert.Equal(FileFormatStatus.Unchanged, formatter.Format(new FileFormatRequest(selected, false, false, log)).Status);
                Assert.Empty(log.Messages);
                Assert.Equal(FileFormatStatus.Unchanged, formatter.Format(new FileFormatRequest(legacy, false, false, log)).Status);
                Assert.Contains("PNFMT004", Assert.Single(log.Messages));
            }
        }

        private sealed class RecordingLog : IFormatterLog
        {
            public List<string> Messages { get; } = new List<string>();

            public void Write(Exception exception) => throw exception;

            public void WriteLine(string message) => this.Messages.Add(message);
        }
    }
}
