// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Xunit;

namespace PNFmt.Tests.Formatter.Xml
{
    public sealed class XmlTagSpacingTests
    {
        [Fact]
        public void Attribute_only_pass_formats_text_tags_but_preserves_xml_space_subtrees()
        {
            const string Protected = "<p xml:space='preserve' a = '1'><c xml:space='default' a = '2'/></p>";
            const string Input = "<r a = '0'><p a = '1'> one<c a = '2'/> two</p>" + Protected + "</r>";
            var settings = new Dictionary<string, string> { ["xml_spaces_around_eq_in_attribute"] = "false", ["xml_space_before_self_closing"] = "true" };
            var result = XmlDocumentFormatter.WrapAttributes(Input, settings, "  ", "\n", 2);
            Assert.Equal("<r a='0'><p a='1'> one<c a='2' /> two</p>" + Protected + "</r>", result);
            Assert.True(XNode.DeepEquals(XDocument.Parse(Input, LoadOptions.PreserveWhitespace), XDocument.Parse(result, LoadOptions.PreserveWhitespace)));
            Assert.Equal(result, XmlDocumentFormatter.WrapAttributes(result, settings, "  ", "\n", 2));
        }

        [Theory]
        [InlineData("<r/>", "<r />", "<r/>")]
        [InlineData("<r   ></r>", "<r   ></r>", "<r   ></r>")]
        [InlineData("<r a='1'\r\n  />", "<r a='1'\r\n  />", "<r a='1'\r\n  />")]
        [InlineData("<r a='1'\n >text</r>", "<r a='1'\n >text</r>", "<r a='1'\n >text</r>")]
        public void Delimiter_spacing_handles_empty_tags_and_preserves_existing_delimiter_lines(string input, string space, string noSpace)
        {
            Assert.Equal(space, Format(input, ("xml_space_after_last_attribute", "true"), ("xml_space_before_self_closing", "true")));
            Assert.Equal(noSpace, Format(input, ("xml_space_after_last_attribute", "false"), ("xml_space_before_self_closing", "false")));
        }

        [Fact]
        public void Do_not_touch_arrangement_still_allows_explicit_spacing()
        {
            Assert.Equal("<r  a='1'\n b='2' />", Format("<r  a = '1'\n b = '2'/>",
                ("xml_attribute_style", "do_not_touch"), ("xml_wrap_tags_and_pi", "true"), ("max_line_length", "5"),
                ("xml_spaces_around_eq_in_attribute", "false"), ("xml_space_before_self_closing", "true")));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Mixed_content_descendants_keep_all_content_whitespace_during_tag_formatting(bool xaml)
        {
            const string Input = "<TextBlock a = '1'> lead <Span a = '2'><Run a = '3'/><Run a = '4'/> <Run a = '5'/></Span> tail\r\n<![CDATA[ x ]]></TextBlock>";
            var options = new Dictionary<string, string> { ["xml_attribute_style"] = "on_different_lines", ["xml_spaces_around_eq_in_attribute"] = "false" };
            var result = XmlDocumentFormatter.Format(Input, options, xaml);
            Assert.True(XNode.DeepEquals(XDocument.Parse(Input, LoadOptions.PreserveWhitespace), XDocument.Parse(result, LoadOptions.PreserveWhitespace)));
            Assert.Contains("<Span\r\n        a='2'><Run\r\n            a='3'/><Run", result);
            Assert.Contains("a='4'/> <Run", result);
            Assert.Contains("</Span> tail\r\n<![CDATA[ x ]]>", result);
            Assert.Equal(result, XmlDocumentFormatter.Format(result, options, xaml));
        }

        [Fact]
        public void Spacing_does_not_rewrite_quotes_entities_values_or_processing_instructions()
        {
            const string Declaration = "<?xml version = '1.0' ?>";
            const string Instruction = "<?data a = '1'?>";
            const string Value = "'first\r\n  second &quot; &#x41; &amp; = > /'";
            var result = Format(Declaration + Instruction + "<r a \n = \t " + Value + " b = \"&apos;\"><![CDATA[ x\r\n ]]><!-- y --></r>",
                ("xml_spaces_around_eq_in_attribute", "false"), ("xml_space_after_last_attribute", "true"), ("xml_space_before_self_closing", "false"));
            Assert.StartsWith(Declaration + "\r\n" + Instruction + "\r\n", result);
            Assert.Contains("a=" + Value + " b=\"&apos;\" >", result);
            Assert.Contains("<![CDATA[ x\r\n ]]><!-- y -->", result);
        }

        [Theory]
        [InlineData("xml")]
        [InlineData("xaml")]
        [InlineData("csproj")]
        public void Spacing_is_shared_by_file_formatters_but_requires_processing_and_layout(string kind)
        {
            using (var directory = new TestDirectory())
            {
                var path = directory.Write("Sample." + kind, "<Project Sdk='Example'/>");
                const string Options = "\nxml_spaces_around_eq_in_attribute = true\nxml_space_before_self_closing = false\nxml_space_after_last_attribute = true\n";
                var config = "root = true\n[*]\npnfmt_formatter = " + kind + Options;
                IFileFormatter formatter = kind == "xml" ? new XmlFormatter() : kind == "xaml" ? new XamlFormatter() : new CsProjFormatter();
                directory.Write(".editorconfig", "root = true\n[*]" + Options);
                Assert.Equal(FileFormatStatus.Skipped, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
                directory.Write(".editorconfig", config + "pnfmt_enabled = true\npnfmt_format = false\n");
                Assert.Equal(FileFormatStatus.Unchanged, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
                directory.Write(".editorconfig", config + "pnfmt_enabled = true\n");
                Assert.Equal(FileFormatStatus.Updated, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
                Assert.Contains("<Project Sdk = ", File.ReadAllText(path));
                Assert.DoesNotContain(" />", File.ReadAllText(path));
                Assert.Equal(FileFormatStatus.Unchanged, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
            }
        }

        [Theory]
        [InlineData("xml_spaces_around_eq_in_attribute", "<r a  =  '1'/>", "<r a='1'/>", "<r a = '1'/>")]
        [InlineData("xml_space_after_last_attribute", "<r a='1'  >text</r>", "<r a='1'>text</r>", "<r a='1' >text</r>")]
        [InlineData("xml_space_before_self_closing", "<r a='1'  />", "<r a='1'/>", "<r a='1' />")]
        public void Spacing_settings_accept_both_values_and_aliases_with_consistent_precedence(string key, string input, string noSpace, string space)
        {
            Assert.Equal(noSpace, Format(input, (key, "false")));
            Assert.Equal(space, Format(input, (key, "true")));
            Assert.Equal(space, Format(input, ("resharper_" + key, " TRUE ")));
            Assert.Equal(noSpace, Format(input, (key, "false"), ("resharper_" + key, "true")));
            Assert.Equal(space, Format(input, (key, "unset"), ("resharper_" + key, "true")));
            Assert.Equal(input, Format(input));
            Assert.Equal(input, Format(input, (key, "invalid"), ("resharper_" + key, "true")));
            Assert.Equal(input, Format(input, (key, "unset"), ("resharper_" + key, "unset")));
        }

        [Fact]
        public void Wrapping_measures_the_normalized_closing_delimiter_spacing()
        {
            Assert.Equal("<r a='1' b='2'/>", Format("<r a='1' b='2'     />", ("xml_space_before_self_closing", "false"),
                ("xml_wrap_tags_and_pi", "true"), ("max_line_length", "16")));
            Assert.Equal("<r a='1'\n    b='2' />", Format("<r a='1' b='2'/>", ("xml_space_before_self_closing", "true"),
                ("xml_wrap_tags_and_pi", "true"), ("max_line_length", "16")));
        }

        [Theory]
        [InlineData("false", "16", "<r a='1' b='2'/>")]
        [InlineData("true", "20", "<r a = '1' b = '2'/>")]
        [InlineData("true", "19", "<r a = '1'\n    b = '2'/>")]
        public void Wrapping_measures_the_normalized_equals_spacing(string spacing, string width, string expected)
        {
            Assert.Equal(expected, Format("<r a  =  '1' b  =  '2'/>", ("xml_spaces_around_eq_in_attribute", spacing),
                ("xml_wrap_tags_and_pi", "true"), ("max_line_length", width)));
        }

        [Fact]
        public void Xaml_inline_containers_preserve_separators_without_literal_text()
        {
            const string Input = "<TextBlock><Run Text='One'/><Run Text='Two'/> <Run Text='Three'/></TextBlock>";
            var settings = new Dictionary<string, string> { ["xml_attribute_style"] = "on_different_lines", ["xml_space_before_self_closing"] = "true" };
            var result = XmlDocumentFormatter.Format(Input, settings, xaml: true);
            Assert.True(XNode.DeepEquals(XDocument.Parse(Input, LoadOptions.PreserveWhitespace), XDocument.Parse(result, LoadOptions.PreserveWhitespace)));
            Assert.Contains("Text='One' /><Run", result);
            Assert.Contains("Text='Two' /> <Run", result);
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
