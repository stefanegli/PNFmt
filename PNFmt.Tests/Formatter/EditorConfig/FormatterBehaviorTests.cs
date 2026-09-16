// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using System.Linq;
using PNFmt.Cli;
using Xunit;

namespace PNFmt.Tests.Formatter.EditorConfig
{
    public sealed class FormatterBehaviorTests
    {
        [Theory]
        [MemberData(nameof(FormatterContractTests.ActivationCases), MemberType = typeof(FormatterContractTests))]
        public void Master_switch_disables_all_behaviors_and_lint(string name, string fileName, string input)
        {
            using var directory = new TestDirectory();
            Configure(directory, name, "pnfmt_enabled = false\npnfmt_format = true\npnfmt_sort_entries = true\ncharset = utf-8-bom\n");
            var path = directory.Write(fileName, input);
            var original = File.ReadAllBytes(path);
            var formatter = FormatterCatalog.CreateDefault().Formatters.Single(item => item.Name == name);

            foreach (var lint in new[] { false, true })
            {
                var result = formatter.Format(new FileFormatRequest(path, true, lint, NullFormatterLog.Instance));
                Assert.Equal(FileFormatStatus.Skipped, result.Status);
                Assert.Empty(result.Diagnostics);
                Assert.Equal(original, File.ReadAllBytes(path));
            }
        }

        [Theory]
        [MemberData(nameof(FormatterContractTests.ActivationCases), MemberType = typeof(FormatterContractTests))]
        public void Disabling_layout_and_leaving_optional_behaviors_off_preserves_bytes(string name, string fileName, string input)
        {
            using var directory = new TestDirectory();
            Configure(directory, name, "pnfmt_format = false\nindent_size = 8\ninsert_final_newline = true\n");
            var path = directory.Write(fileName, input);
            var original = File.ReadAllBytes(path);
            var result = Run(path);

            Assert.Null(result.Error);
            Assert.Equal(FileFormatStatus.Unchanged, result.Result.Status);
            Assert.Empty(result.Result.Diagnostics);
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.DoesNotContain(result.LogMessages, message => message.Contains("PNFMT004"));
        }

        [Theory]
        [InlineData("ini", "Data.ini", "z=2\na=1", "a=1\nz=2")]
        [InlineData("rsp", "Data.rsp", "  z.cs  \r\n\n a.cs ", " a.cs \r\n\n  z.cs  ")]
        [InlineData("csharp", "Data.cs", "using Z;\nusing A;\nclass C{void M(){}}", "using A;\nusing Z;\nclass C{void M(){}}")]
        public void Sorting_can_run_without_layout_changes(string name, string fileName, string input, string expected)
        {
            using var directory = new TestDirectory();
            Configure(directory, name, "pnfmt_format = false\npnfmt_sort_entries = true\n");
            var path = directory.Write(fileName, input);

            var result = Run(path);

            Assert.Null(result.Error);
            Assert.Equal(FileFormatStatus.Updated, result.Result.Status);
            Assert.Empty(result.Result.Diagnostics);
            Assert.Equal(expected, File.ReadAllText(path));
            Assert.Equal(FileFormatStatus.Unchanged, Run(path).Result.Status);
        }

        [Theory]
        [InlineData("csproj", "Data.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><Z>2</Z><A>1</A></PropertyGroup></Project>", "<A>", "<Z>")]
        [InlineData("slnx", "Data.slnx", "<Solution><Project Path=\"z.csproj\" /><Project Path=\"a.csproj\" /></Solution>", "a.csproj", "z.csproj")]
        [InlineData("resx", "Data.resx", "<root><resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader><data name=\"z\"><value>2</value></data><data name=\"a\"><value>1</value></data></root>", "name=\"a\"", "name=\"z\"")]
        public void Xml_based_formatters_sort_without_indenting(string name, string fileName, string input, string first, string second)
        {
            using var directory = new TestDirectory();
            Configure(directory, name, "pnfmt_format = false\npnfmt_sort_entries = true\n");
            var path = directory.Write(fileName, input);

            var result = Run(path);
            var formatted = File.ReadAllText(path);

            Assert.Null(result.Error);
            Assert.Equal(FileFormatStatus.Updated, result.Result.Status);
            Assert.True(formatted.IndexOf(first, StringComparison.Ordinal) < formatted.IndexOf(second, StringComparison.Ordinal));
            Assert.DoesNotContain("\n", formatted);
            Assert.Equal(FileFormatStatus.Unchanged, Run(path).Result.Status);
        }

        [Theory]
        [InlineData("pnfmt_csharp_sort_modifiers", "class C{static public void M(){}}", "public static void M(){}")]
        [InlineData("pnfmt_csharp_remove_regions", "class C{\n#region Details\nvoid M(){}\n#endregion\n}", "void M(){}")]
        public void Csharp_cleanup_switches_work_independently_of_layout(string option, string input, string expected)
        {
            using var directory = new TestDirectory();
            Configure(directory, "csharp", "pnfmt_format = false\n" + option + " = true\n");
            directory.Write("nested/.editorconfig", "[*]\n" + option + " = false\n");
            var enabled = directory.Write("Data.cs", input);
            var disabled = directory.Write("nested/Data.cs", input);

            Assert.Equal(FileFormatStatus.Unchanged, Run(disabled).Result.Status);
            Assert.Equal(input, File.ReadAllText(disabled));
            var result = Run(enabled);
            Assert.Null(result.Error);
            Assert.Empty(result.Result.Diagnostics);
            Assert.Equal(FileFormatStatus.Updated, result.Result.Status);
            Assert.Contains("class C{", File.ReadAllText(enabled));
            Assert.Contains(expected, File.ReadAllText(enabled));
            Assert.DoesNotContain("#region", File.ReadAllText(enabled));
            Assert.Equal(FileFormatStatus.Unchanged, Run(enabled).Result.Status);
        }

        [Theory]
        [InlineData("rsp", "Data.rsp", "z.cs  \na.cs", "z.cs", "a.cs")]
        [InlineData("slnx", "Data.slnx", "<Solution><Project Path=\"z.csproj\" /><Project Path=\"a.csproj\" /></Solution>", "z.csproj", "a.csproj")]
        public void Layout_defaults_on_and_sorting_defaults_off(string name, string fileName, string input, string first, string second)
        {
            using var directory = new TestDirectory();
            Configure(directory, name, "");
            var path = directory.Write(fileName, input);

            var result = Run(path);
            var formatted = File.ReadAllText(path);

            Assert.Null(result.Error);
            Assert.Equal(FileFormatStatus.Updated, result.Result.Status);
            Assert.True(formatted.IndexOf(first, StringComparison.Ordinal) < formatted.IndexOf(second, StringComparison.Ordinal));
            Assert.Equal(FileFormatStatus.Unchanged, Run(path).Result.Status);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Ini_prefix_grouping_can_preserve_entry_order(bool formatLayout)
        {
            using var directory = new TestDirectory();
            Configure(directory, "ini", "pnfmt_format = " + formatLayout + "\npnfmt_ini_group_by_prefix = true\npnfmt_sort_entries = false\n");
            var path = directory.Write("Data.ini", "b_z = 1\na_z = 2\nb_a = 3\na_a = 4\n");

            var result = Run(path);

            Assert.Null(result.Error);
            Assert.Equal("b_z = 1\nb_a = 3\n\na_z = 2\na_a = 4\n", File.ReadAllText(path));
            Assert.Equal(FileFormatStatus.Unchanged, Run(path).Result.Status);
        }

        [Theory]
        [InlineData("pnfmt_resx_insert_documentation_comment", "<!--")]
        [InlineData("pnfmt_resx_insert_xsd_schema", "<xsd:schema")]
        public void Resx_insertion_is_independent_from_removal(string option, string marker)
        {
            using var directory = new TestDirectory();
            const string Input = "<root><resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader></root>";
            var path = directory.Write("Data.resx", Input);
            Configure(directory, "resx", "pnfmt_format = false\npnfmt_resx_remove_documentation_comment = false\npnfmt_resx_remove_xsd_schema = false\n");
            Assert.Equal(FileFormatStatus.Unchanged, Run(path).Result.Status);
            Assert.Equal(Input, File.ReadAllText(path));

            Configure(directory, "resx", "pnfmt_format = false\n" + option + " = true\n");
            Assert.Equal(FileFormatStatus.Updated, Run(path).Result.Status);
            Assert.Contains(marker, File.ReadAllText(path));

            Configure(directory, "resx", "pnfmt_format = false\n" + option + " = false\n");
            var inserted = File.ReadAllBytes(path);
            Assert.Equal(FileFormatStatus.Unchanged, Run(path).Result.Status);
            Assert.Equal(inserted, File.ReadAllBytes(path));
        }

        [Fact]
        public void Nested_master_switch_retains_selection_and_behavior_preferences()
        {
            using var directory = new TestDirectory();
            Configure(directory, "rsp", "pnfmt_format = false\npnfmt_sort_entries = true\n");
            directory.Write("nested/.editorconfig", "[*]\npnfmt_enabled = false\n");
            directory.Write("nested/enabled/.editorconfig", "[*]\npnfmt_enabled = true\n");
            var disabled = directory.Write("nested/Data.custom", "z.cs\na.cs");
            var enabled = directory.Write("nested/enabled/Data.custom", "z.cs\na.cs");

            Assert.Equal(FileFormatStatus.Skipped, Run(disabled).Result.Status);
            Assert.Equal("z.cs\na.cs", File.ReadAllText(disabled));
            Assert.Equal(FileFormatStatus.Updated, Run(enabled).Result.Status);
            Assert.Equal("a.cs\nz.cs", File.ReadAllText(enabled));
        }

        [Theory]
        [InlineData("pnfmt_formatter = xml\n")]
        [InlineData("pnfmt_enabled = true\n")]
        [InlineData("pnfmt_enabled = unset\npnfmt_formatter = xml\n")]
        public void Incomplete_activation_remains_compatible_with_a_warning(string settings)
        {
            using var directory = new TestDirectory();
            directory.Write(".editorconfig", "root = true\n[*]\n" + settings);
            var path = directory.Write("Data.xml", "<root/>");

            var result = Run(path);

            Assert.Null(result.Error);
            Assert.NotEqual(FileFormatStatus.Skipped, result.Result.Status);
            Assert.Contains("pnfmt_enabled = true", Assert.Single(result.LogMessages, message => message.Contains("PNFMT004")));
        }

        private static void Configure(TestDirectory directory, string name, string options)
        {
            directory.Write(".editorconfig", "root = true\n[*]\npnfmt_enabled = true\npnfmt_formatter = " + name + "\n" + options);
        }

        private static FileFormattingOutcome Run(string path)
        {
            return new FormattingRunner(FormatterCatalog.CreateDefault()).Run(new[] { path }, true, false, 1).Outcomes[0];
        }
    }
}
