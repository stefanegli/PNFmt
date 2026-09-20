// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Xunit;

namespace PNFmt.Tests.Formatter.Xml
{
    public sealed class XmlAttributeWrappingTests
    {
        [Fact]
        public void Aliases_and_language_specific_width_work_with_clear_precedence()
        {
            const string Input = "<r a='1' b='2'/>";
            Assert.Equal("<r\n    a='1'\n    b='2'/>", Format(Input, ("resharper_xml_attribute_style", "ON_DIFFERENT_LINES")));
            Assert.Equal("<r a='1'\n        b='2'/>", Format(Input, ("xml_attribute_style", "first_attribute_on_single_line"),
                ("resharper_xml_attribute_indent", "double_indent")));
            Assert.Equal("<r a='1'\n    b='2'/>", Format(Input, ("resharper_xml_wrap_tags_and_pi", "TRUE"),
                ("resharper_xml_max_line_length", "12"), ("max_line_length", "100")));
            Assert.Equal(Input, Format(Input, ("xml_wrap_tags_and_pi", "false"), ("resharper_xml_wrap_tags_and_pi", "true"), ("max_line_length", "10")));
            Assert.Equal(Input, Format(Input, ("xml_attribute_style", "invalid"), ("resharper_xml_attribute_style", "on_different_lines")));
            Assert.Equal(Input, Format(Input, ("xml_wrap_tags_and_pi", "true"), ("xml_max_line_length", "invalid"),
                ("resharper_xml_max_line_length", "10"), ("max_line_length", "10")));
            Assert.Equal(Input, Format(Input, ("xml_wrap_tags_and_pi", "true"), ("xml_max_line_length", "120"), ("max_line_length", "10")));
            Assert.Equal("<r a='1'\n    b='2'/>", Format(Input, ("xml_wrap_tags_and_pi", "true"), ("xml_max_line_length", "unset"), ("max_line_length", "12")));
            Assert.Equal(Input, Format(Input, ("resharper_xml_attribute_style", "unset")));
        }
        [Fact]
        public void Alignment_uses_the_actual_first_attribute_column()
        {
            Assert.Equal("<root>\n  <child a='1'\n         b='2'/>\n</root>",
                Format("<root><child a='1' b='2'/></root>", ("indent_size", "2"),
                    ("xml_attribute_style", "first_attribute_on_single_line"), ("xml_attribute_indent", "align_by_first_attribute")));
        }

        [Fact]
        public void Attribute_indentation_alone_does_not_change_arrangement()
        {
            const string Input = "<r a='1' b='2'/>";
            Assert.Equal(Input, Format(Input, ("xml_attribute_indent", "double_indent")));
        }

        [Fact]
        public void Attribute_only_wrapping_does_not_add_the_xml_layout_depth_limit_to_msbuild()
        {
            var input = string.Concat(Enumerable.Repeat("<r a='1'>", 257)) + string.Concat(Enumerable.Repeat("</r>", 257));
            var options = new Dictionary<string, string> { ["xml_attribute_style"] = "on_different_lines" };
            var result = XmlDocumentFormatter.WrapAttributes(input, options, "  ", "\n", 2);
            Assert.True(XNode.DeepEquals(XDocument.Parse(input), XDocument.Parse(result)));
            Assert.Equal(result, XmlDocumentFormatter.WrapAttributes(result, options, "  ", "\n", 2));
        }

        [Theory]
        [InlineData("on_single_line", "<r a='1' b='2'/>")]
        [InlineData("first_attribute_on_single_line", "<r a='1'\n    b='2'/>")]
        [InlineData("on_different_lines", "<r\n    a='1'\n    b='2'/>")]
        [InlineData("do_not_touch", "<r  a='1'\n b='2'/>")]
        [InlineData("invalid", "<r  a='1'\n b='2'/>")]
        public void Attribute_styles_control_layout_without_a_width(string style, string expected)
        {
            Assert.Equal(expected, Format("<r  a='1'\n b='2'/>", ("xml_attribute_style", style)));
        }

        [Fact]
        public void Attribute_values_entities_quotes_and_spacing_around_equals_remain_exact()
        {
            const string Value = "data \n = 'long\r\n    value &quot; &#x41; > / &amp;'";
            var result = Format("<root " + Value + " other=\"&apos;\"/>", ("xml_attribute_style", "on_different_lines"),
                ("max_line_length", "8"), ("xml_wrap_tags_and_pi", "true"));
            Assert.Contains(Value, result);
            Assert.Contains("other=\"&apos;\"", result);
        }

        [Fact]
        public void Comments_and_ordinary_processing_instructions_remain_opaque()
        {
            const string Input = "<!-- <r a='1' b='2'/> -->\n<?instruction first='long value' second='value'?>\n<r/>";
            Assert.Equal(Input, Format(Input, ("xml_attribute_style", "on_different_lines"),
                ("max_line_length", "8"), ("xml_wrap_tags_and_pi", "true")));
        }

        [Fact]
        public void Default_wrapping_retains_existing_attribute_breaks_and_closing_delimiter_layout()
        {
            const string Input = "<root\n  first='1'\n  second='2'\n/>";
            Assert.Equal(Input, Format(Input, ("xml_wrap_tags_and_pi", "true"), ("max_line_length", "120")));
            Assert.Equal("<r/>", Format("<r/>", ("xml_attribute_style", "on_different_lines")));
            Assert.Equal("<r\n    a='1'/>", Format("<r a='1'/>", ("xml_attribute_style", "on_different_lines")));
        }

        [Theory]
        [InlineData("xml", false)]
        [InlineData("xml", true)]
        [InlineData("xaml", false)]
        [InlineData("xaml", true)]
        [InlineData("csproj", false)]
        [InlineData("csproj", true)]
        public void Encoding_declaration_changes_are_wrapped_on_the_first_pass(string kind, bool hasDeclaration)
        {
            using (var directory = new TestDirectory())
            {
                var path = directory.Write("Sample." + kind,
                    (hasDeclaration ? "<?xml version='1.0' standalone='yes'?>\n" : string.Empty) + "<Project a='1' b='2'/>");
                var original = File.ReadAllBytes(path);
                directory.Write(".editorconfig", "root = true\n[*]\npnfmt_enabled = true\npnfmt_formatter = " + kind
                    + "\ncharset = utf-16le\nend_of_line = lf\nindent_size = 4\nmax_line_length = 25\nxml_wrap_tags_and_pi = true\n");
                IFileFormatter formatter = kind == "xml" ? new XmlFormatter() : kind == "xaml" ? new XamlFormatter() : new CsProjFormatter();
                Assert.Equal(FileFormatStatus.Updated, formatter.Format(new FileFormatRequest(path, false, false, NullFormatterLog.Instance)).Status);
                Assert.Equal(original, File.ReadAllBytes(path));
                Assert.Equal(FileFormatStatus.Updated, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
                Assert.Contains("\n    encoding=", File.ReadAllText(path));
                Assert.Equal(new byte[] { 0xff, 0xfe }, File.ReadAllBytes(path).Take(2));
                Assert.Equal(FileFormatStatus.Unchanged, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
            }
        }

        [Theory]
        [InlineData("xml", "Data.xml")]
        [InlineData("xaml", "View.xaml")]
        [InlineData("csproj", "Project.csproj")]
        [InlineData("csproj", "Build.props")]
        [InlineData("csproj", "Build.targets")]
        [InlineData("csproj", "Build.proj")]
        public void File_formatters_support_preview_disabled_layout_and_idempotence(string kind, string name)
        {
            using (var directory = new TestDirectory())
            {
                const string Input = "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><PackageReference Include=\"Some.Package\" Version=\"1.0.0\" PrivateAssets=\"all\" /></ItemGroup></Project>";
                var path = directory.Write(name, Input);
                var config = "root = true\n[*]\npnfmt_enabled = true\npnfmt_formatter = " + kind
                    + "\nxml_attribute_style = on_different_lines\nend_of_line = lf\nindent_size = 2\n";
                directory.Write(".editorconfig", config + "pnfmt_format = false\n");
                IFileFormatter formatter = kind == "xml" ? new XmlFormatter() : kind == "xaml" ? new XamlFormatter() : new CsProjFormatter();
                Assert.Equal(FileFormatStatus.Unchanged, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
                Assert.Equal(Input, File.ReadAllText(path));
                directory.Write(".editorconfig", config);
                Assert.Equal(FileFormatStatus.Updated, formatter.Format(new FileFormatRequest(path, false, false, NullFormatterLog.Instance)).Status);
                Assert.Equal(Input, File.ReadAllText(path));
                Assert.Equal(FileFormatStatus.Updated, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
                var result = File.ReadAllText(path);
                Assert.Contains("<PackageReference\n      Include=\"Some.Package\"\n      Version=\"1.0.0\"\n      PrivateAssets=\"all\" />", result);
                Assert.True(XNode.DeepEquals(XDocument.Parse(Input), XDocument.Parse(result)));
                Assert.Equal(FileFormatStatus.Unchanged, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
            }
        }

        [Theory]
        [InlineData("single_indent", "<r\n    a='1'\n    b='2'/>")]
        [InlineData("double_indent", "<r\n        a='1'\n        b='2'/>")]
        [InlineData("align_by_first_attribute", "<r\n    a='1'\n    b='2'/>")]
        [InlineData("invalid", "<r\n    a='1'\n    b='2'/>")]
        public void Indentation_styles_apply_to_chopped_attributes(string indent, string expected)
        {
            Assert.Equal(expected, Format("<r a='1' b='2'/>", ("xml_attribute_style", "on_different_lines"), ("xml_attribute_indent", indent)));
        }

        [Theory]
        [InlineData("invalid")]
        [InlineData("0")]
        [InlineData("1000000")]
        public void Invalid_tab_width_uses_the_formatter_indentation_width(string width)
        {
            Assert.Equal("<r>\n\t<child a='1'\n\t\tb='2'/>\n</r>", Format("<r><child a='1' b='2'/></r>",
                ("indent_style", "tab"), ("tab_width", width), ("xml_wrap_tags_and_pi", "true"), ("max_line_length", "20")));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("unset")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("invalid")]
        [InlineData("2147483648")]
        public void Missing_or_invalid_width_does_not_wrap(string width)
        {
            var settings = new List<(string, string)> { ("xml_wrap_tags_and_pi", "true") };
            if (width is not null)
            {
                settings.Add(("max_line_length", width));
            }
            const string Input = "<root first='value' second='value'/>";
            Assert.Equal(Input, Format(Input, settings.ToArray()));
        }

        [Fact]
        public void Msbuild_documents_with_a_dtd_retain_the_existing_serializer_behavior()
        {
            using (var directory = new TestDirectory())
            {
                var path = directory.Write("Sample.csproj", "<!DOCTYPE Project [<!ENTITY value 'name'>]><Project Sdk='&value;' Other='value'/>");
                directory.Write(".editorconfig", "root = true\n[*]\npnfmt_enabled = true\npnfmt_formatter = csproj\nxml_attribute_style = on_different_lines\n");
                var result = new CsProjFormatter().Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance));
                Assert.Equal(FileFormatStatus.Updated, result.Status);
                Assert.Contains("<Project Sdk=\"name\" Other=\"value\" />", File.ReadAllText(path));
            }
        }

        [Fact]
        public void Msbuild_wrapping_preserves_conditions_task_arguments_and_property_text_while_sorting()
        {
            using (var directory = new TestDirectory())
            {
                const string Condition = "'$(Configuration)' == 'Release' And Exists('a &gt; b')";
                const string Command = "echo &quot;first &amp; second&quot;";
                var input = "<Project><PropertyGroup><Zulu>two words;three words</Zulu><Alpha>alpha</Alpha></PropertyGroup>"
                    + "<Target Name='Build' Condition=\"" + Condition + "\"><Exec Command='" + Command + "' WorkingDirectory='some long path'/></Target></Project>";
                var path = directory.Write("Project.csproj", input);
                directory.Write(".editorconfig", "root = true\n[*.csproj]\npnfmt_enabled = true\npnfmt_formatter = csproj\n"
                    + "pnfmt_sort_entries = true\nmax_line_length = 35\nxml_wrap_tags_and_pi = true\nend_of_line = lf\n");
                var formatter = new CsProjFormatter();
                Assert.Equal(FileFormatStatus.Updated, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
                var result = File.ReadAllText(path);
                var original = XDocument.Parse(input);
                var formatted = XDocument.Parse(result);
                Assert.Equal(new[] { "Alpha", "Zulu" }, formatted.Root.Element("PropertyGroup").Elements().Select(element => element.Name.LocalName));
                Assert.Equal(original.Root.Element("PropertyGroup").Element("Zulu").Value, formatted.Root.Element("PropertyGroup").Element("Zulu").Value);
                Assert.True(XNode.DeepEquals(original.Root.Element("Target"), formatted.Root.Element("Target")));
                Assert.Contains("<Target Name=\"Build\"\n", result);
                Assert.Contains("Condition=\"" + Condition + "\"", result);
                Assert.Equal(FileFormatStatus.Unchanged, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
            }
        }

        [Fact]
        public void Multiple_attributes_fit_on_a_continuation_line()
        {
            Assert.Equal("<root first='long'\n    b='2' c='3'/>", Format("<root first='long' b='2' c='3'/>",
                ("xml_wrap_tags_and_pi", "true"), ("max_line_length", "20")));
        }

        [Theory]
        [InlineData("lf", "\n")]
        [InlineData("crlf", "\r\n")]
        [InlineData("cr", "\r")]
        public void Newlines_tabs_and_tab_stops_are_honored(string style, string newline)
        {
            var result = Format("<r><child a='1' b='2'/></r>", ("indent_style", "tab"), ("indent_size", "tab"),
                ("tab_width", "4"), ("end_of_line", style), ("xml_wrap_tags_and_pi", "true"), ("max_line_length", "20"));
            Assert.Equal("<r>" + newline + "\t<child a='1'" + newline + "\t\tb='2'/>" + newline + "</r>", result);
            result = Format("<r><child a='1' b='2'/></r>", ("indent_style", "tab"), ("tab_width", "4"),
                ("end_of_line", style), ("xml_attribute_style", "first_attribute_on_single_line"),
                ("xml_attribute_indent", "align_by_first_attribute"));
            Assert.Contains(newline + new string(' ', 11) + "b='2'/>", result);
        }

        [Theory]
        [InlineData("<p a='1' b='2'>text</p>")]
        [InlineData("<p a='1' b='2'>one<b a='1' b='2'/>two</p>")]
        [InlineData("<p a='1' b='2'> \n </p>")]
        [InlineData("<p a='1' b='2'><![CDATA[ < & > ]]></p>")]
        [InlineData("<p xml:space='preserve' a='1' b='2'><c xml:space='default' a='1'/></p>")]
        public void Protected_subtrees_remain_exact_when_wrapping_is_requested(string content)
        {
            var result = Format("<root>" + content + "<other a='1' b='2'/></root>", ("xml_attribute_style", "on_different_lines"));
            Assert.Contains(content, result);
            Assert.Contains("<other\n        a='1'", result);
        }

        [Fact]
        public void Single_line_style_can_join_existing_breaks_then_wrap_to_width()
        {
            Assert.Equal("<root a='1'\n    b='2'/>", Format("<root\n a='1'\n b='2'/>",
                ("xml_attribute_style", "on_single_line"), ("xml_wrap_tags_and_pi", "true"), ("max_line_length", "18")));
        }

        [Theory]
        [InlineData("30", "<root a='111' b='222' c='3'/>")]
        [InlineData("29", "<root a='111' b='222' c='3'/>")]
        [InlineData("28", "<root a='111' b='222'\n    c='3'/>")]
        [InlineData("16", "<root a='111'\n    b='222'\n    c='3'/>")]
        public void Width_wrapping_fits_complete_attributes_and_counts_the_closing_delimiter(string width, string expected)
        {
            Assert.Equal(expected, Format("<root a='111' b='222' c='3'/>", ("max_line_length", width), ("xml_wrap_tags_and_pi", "true")));
        }

        [Theory]
        [InlineData("xml", "Sample.xml")]
        [InlineData("csproj", "Sample.csproj")]
        public void Wrapping_options_do_not_activate_processing_and_unset_allows_an_alias(string kind, string name)
        {
            using (var directory = new TestDirectory())
            {
                const string Input = "<Project Sdk='Example' Other='Value'/>";
                var path = directory.Write(name, Input);
                const string Options = "xml_attribute_style = do_not_touch\nresharper_xml_attribute_style = on_different_lines\n";
                directory.Write(".editorconfig", "root = true\n[*]\n" + Options);
                IFileFormatter formatter = kind == "xml" ? new XmlFormatter() : new CsProjFormatter();
                Assert.Equal(FileFormatStatus.Skipped, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
                path = directory.Write("nested/" + name, Input);
                directory.Write("nested/.editorconfig", "[*]\npnfmt_enabled = true\npnfmt_formatter = " + kind + "\nxml_attribute_style = unset\nend_of_line = lf\n");
                Assert.Equal(FileFormatStatus.Updated, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
                Assert.StartsWith("<Project\n", File.ReadAllText(path));
            }
        }

        [Theory]
        [InlineData("false")]
        [InlineData("unset")]
        [InlineData("invalid")]
        public void Wrapping_requires_the_explicit_switch_and_do_not_touch_wins_for_tags(string value)
        {
            const string Input = "<root first='value' second='value'/>";
            Assert.Equal(Input, Format(Input, ("max_line_length", "10")));
            Assert.Equal(Input, Format(Input, ("max_line_length", "10"), ("xml_wrap_tags_and_pi", value)));
            Assert.Equal(Input, Format(Input, ("max_line_length", "10"), ("xml_wrap_tags_and_pi", "true"), ("xml_attribute_style", "do_not_touch")));
        }

        [Fact]
        public void Xaml_text_containers_are_protected_while_structural_attributes_can_wrap()
        {
            const string Input = "<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='Keep' Width='10'/><Button Width='10' Height='20'/></Grid>";
            var settings = new Dictionary<string, string> { ["xml_attribute_style"] = "on_different_lines" };
            var result = XmlDocumentFormatter.Format(Input, settings, xaml: true);
            Assert.Contains("<TextBlock Text='Keep' Width='10'/>", result);
            Assert.Contains("<Button\n        Width='10'\n        Height='20'/>", result);
            Assert.Equal(result, XmlDocumentFormatter.Format(result, settings, xaml: true));
            Assert.True(XNode.DeepEquals(XDocument.Parse(Input), XDocument.Parse(result)));
        }

        [Fact]
        public void Xml_declaration_can_wrap_without_changing_its_values_or_applying_tag_style()
        {
            const string Input = "<?xml version='1.0' encoding='utf-8'?>\n<r/>";
            Assert.Equal("<?xml version='1.0'\n    encoding='utf-8'?>\n<r/>", Format(Input, ("max_line_length", "25"), ("xml_wrap_tags_and_pi", "true")));
            Assert.Equal(Input, Format(Input, ("xml_attribute_style", "on_different_lines")));
        }

        private static string Format(string input, params (string Key, string Value)[] settings)
        {
            var options = settings.ToDictionary(item => item.Key, item => item.Value);
            var result = XmlDocumentFormatter.Format(input, options);
            Assert.True(XNode.DeepEquals(XDocument.Parse(input), XDocument.Parse(result)));
            Assert.Equal(result, XmlDocumentFormatter.Format(result, options));
            return result;
        }
    }
}
