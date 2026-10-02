// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Xunit;

namespace PNFmt.Tests.Formatter.Xml
{
    public sealed class XmlQuoteStyleTests
    {
        [Theory]
        [InlineData("xml")]
        [InlineData("xaml")]
        [InlineData("csproj")]
        public void File_configuration_can_unset_an_inherited_style_and_requires_enabled_layout(string kind)
        {
            using (var directory = new TestDirectory())
            {
                const string Input = "<Project Sdk=\"Example\" Condition=\"'a' == 'b'\"/>";
                var path = directory.Write("nested/Sample." + kind, Input);
                directory.Write(".editorconfig", "root = true\n[*]\npnfmt_xml_quote_style = single_quotes\n");
                IFileFormatter formatter = kind == "xml" ? new XmlFormatter() : kind == "xaml" ? new XamlFormatter() : new CsProjFormatter();
                Assert.Equal(FileFormatStatus.Skipped, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
                var config = "[*]\npnfmt_enabled = true\npnfmt_formatter = " + kind + "\n";
                directory.Write("nested/.editorconfig", config + "pnfmt_format = false\n");
                Assert.Equal(FileFormatStatus.Unchanged, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
                Assert.Equal(Input, File.ReadAllText(path));
                directory.Write("nested/.editorconfig", config + "pnfmt_xml_quote_style = unset\n");
                formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance));
                Assert.Contains("Sdk=\"Example\"", File.ReadAllText(path));
                directory.Write("nested/.editorconfig", config);
                Assert.Equal(FileFormatStatus.Updated, formatter.Format(new FileFormatRequest(path, false, false, NullFormatterLog.Instance)).Status);
                Assert.Contains("Sdk=\"Example\"", File.ReadAllText(path));
                Assert.Equal(FileFormatStatus.Updated, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
                var result = File.ReadAllText(path);
                Assert.Contains("Sdk='Example'", result);
                Assert.Contains("Condition='&apos;a&apos; == &apos;b&apos;'", result);
                Assert.True(XNode.DeepEquals(XDocument.Parse(Input), XDocument.Parse(result)));
                Assert.Equal(FileFormatStatus.Unchanged, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
            }
        }

        [Theory]
        [InlineData("<r a='&unknown;'/>")]
        [InlineData("<r a='bad & value'/>")]
        [InlineData("<r a='&#x0;'/>")]
        [InlineData("<r a=\"bad\\\"quote\"/>")]
        [InlineData("<!DOCTYPE r [<!ENTITY example 'value'>]><r a='&example;'/>")]
        public void Invalid_xml_and_dtds_are_skipped_without_attempting_quote_repair(string input)
        {
            using (var directory = new TestDirectory())
            {
                var path = directory.Write("Sample.xml", input);
                directory.Write(".editorconfig", "root = true\n[*]\npnfmt_enabled = true\npnfmt_formatter = xml\npnfmt_xml_quote_style = double_quotes\n");
                var result = new XmlFormatter().Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance));
                Assert.Equal(FileFormatStatus.Skipped, result.Status);
                Assert.Equal("XML001", Assert.Single(result.Diagnostics).Code);
                Assert.Equal(input, File.ReadAllText(path));
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("preserve")]
        [InlineData("unset")]
        [InlineData("invalid")]
        [InlineData("")]
        public void Missing_or_non_normalizing_styles_preserve_quotes_even_with_other_tag_options(string style)
        {
            const string Input = "<r a='&quot;one&quot;' b=\"'two'\"/>";
            var options = new List<(string Key, string Value)> { ("xml_attribute_style", "on_single_line") };
            if (style is not null)
            {
                options.Add(("pnfmt_xml_quote_style", style));
            }
            Assert.Equal(Input, Format(Input, options.ToArray()));
        }

        [Theory]
        [InlineData("double_quotes", "<r a='say \"hello\"'/>", "<r a=\"say &quot;hello&quot;\"/>")]
        [InlineData("single_quotes", "<r a=\"it's ready\"/>", "<r a='it&apos;s ready'/>")]
        [InlineData("double_quotes", "<r a='\"\"'/>", "<r a=\"&quot;&quot;\"/>")]
        [InlineData("single_quotes", "<r a=\"''\"/>", "<r a='&apos;&apos;'/>")]
        [InlineData("double_quotes", "<r a=''/>", "<r a=\"\"/>")]
        [InlineData("single_quotes", "<r a=\"\"/>", "<r a=''/>")]
        [InlineData(" DOUBLE_QUOTES ", "<r a='one' b=\"two\"/>", "<r a=\"one\" b=\"two\"/>")]
        [InlineData("double_quotes", "<r a='backslash\\\"'/>", "<r a=\"backslash\\&quot;\"/>")]
        [InlineData("single_quotes", "<r a=\"backslash\\'\"/>", "<r a='backslash\\&apos;'/>")]
        public void Only_conflicting_literal_quotes_are_escaped(string style, string input, string expected)
        {
            Assert.Equal(expected, Format(input, ("pnfmt_xml_quote_style", style)));
        }

        [Fact]
        public void Pnfmt_quote_option_has_no_invented_jetbrains_alias()
        {
            const string Input = "<r a='one'/>";
            Assert.Equal(Input, Format(Input, ("resharper_pnfmt_xml_quote_style", "double_quotes"), ("xml_quote_style", "double_quotes")));
        }

        [Fact]
        public void Quote_changes_preserve_attribute_spacing_and_work_with_explicit_spacing()
        {
            const string Input = "<r  a \n = \t 'one'\n b = 'two'  />";
            Assert.Equal("<r  a \n = \t \"one\"\n b = \"two\"  />", Format(Input, ("pnfmt_xml_quote_style", "double_quotes")));
            Assert.Equal("<r  a=\"one\"\n b=\"two\" />", Format(Input, ("pnfmt_xml_quote_style", "double_quotes"),
                ("xml_attribute_style", "do_not_touch"), ("xml_spaces_around_eq_in_attribute", "false"), ("xml_space_before_self_closing", "true")));
        }

        [Theory]
        [InlineData("single_quotes", '"', '\'')]
        [InlineData("double_quotes", '\'', '"')]
        public void References_unicode_and_physical_whitespace_keep_their_spelling_and_runtime_value(string style, char sourceQuote, char targetQuote)
        {
            const string References = "&amp; &lt; &gt; &quot; &apos; &#34; &#x22; &#39; &#x27; &#00034; &#x000022; &amp;quot; &amp;#34;";
            const string Whitespace = "\r\n\t &#xA;&#xD;&#x9;&#10;&#13;&#9; ";
            var input = "<r a=" + sourceQuote + targetQuote + "\\ Unicode \u00e9 \U0001f600 " + References + Whitespace + targetQuote + sourceQuote + "/>";
            var result = Format(input, ("pnfmt_xml_quote_style", style));
            Assert.Contains(References, result);
            Assert.Contains(Whitespace, result);
            Assert.Contains("\\ Unicode \u00e9 \U0001f600 ", result);
            Assert.Contains("\n\r\t\n\r\t", XDocument.Parse(result).Root.Attribute("a").Value);
        }

        [Fact]
        public void Selected_quote_style_does_not_unescape_existing_references()
        {
            const string Input = "<r a=\"&quot;&#34;&#x22;&apos;&#39;&#x27;&amp;quot;\"/>";
            Assert.Equal(Input, Format(Input, ("pnfmt_xml_quote_style", "double_quotes")));
            Assert.Equal("<r a='&quot;&#34;&#x22;&apos;&#39;&#x27;&amp;quot;'/>", Format(Input, ("pnfmt_xml_quote_style", "single_quotes")));
        }

        [Fact]
        public void Text_comments_cdata_processing_instructions_and_declarations_are_untouched()
        {
            const string Declaration = "<?xml version='1.0'?>";
            const string Content = "He said \"hello\" &quot; 'yes' <b a='&quot;'/><![CDATA[ 'raw' \"raw\" ]]><!-- 'comment' --><?data a='value'?>";
            var result = Format(Declaration + "\n<r a='value'>" + Content + "</r>", ("pnfmt_xml_quote_style", "double_quotes"));
            Assert.Equal(Declaration + "\n<r a=\"value\">" + Content.Replace("<b a='&quot;'/>", "<b a=\"&quot;\"/>") + "</r>", result);
        }

        [Theory]
        [InlineData("23", "<r a=\"&quot;\" b=\"two\"/>")]
        [InlineData("22", "<r a=\"&quot;\"\n    b=\"two\"/>")]
        public void Wrapping_measures_expanded_escapes_on_the_first_pass(string width, string expected)
        {
            Assert.Equal(expected, Format("<r a='\"' b='two'/>", ("pnfmt_xml_quote_style", "double_quotes"),
                ("xml_wrap_tags_and_pi", "true"), ("max_line_length", width)));
        }

        [Fact]
        public void Xaml_markup_extensions_and_inline_separators_keep_their_decoded_values()
        {
            const string Input = "<TextBlock xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" Text=\"{Binding Caption, FallbackValue='A &amp; B'}\"><Run Text=\"'first'\"/><Run Text=\"second\"/> <Run Text=\"third\"/></TextBlock>";
            var settings = new Dictionary<string, string> { ["pnfmt_xml_quote_style"] = "single_quotes" };
            var result = XmlDocumentFormatter.Format(Input, settings, xaml: true);
            Assert.Contains("Text='{Binding Caption, FallbackValue=&apos;A &amp; B&apos;}'", result);
            Assert.Contains("<Run Text='&apos;first&apos;'/><Run Text='second'/> <Run Text='third'/>", result);
            Assert.True(XNode.DeepEquals(XDocument.Parse(Input, LoadOptions.PreserveWhitespace), XDocument.Parse(result, LoadOptions.PreserveWhitespace)));
            Assert.Equal(result, XmlDocumentFormatter.Format(result, settings, xaml: true));
        }

        [Fact]
        public void Xml_space_boundaries_preserve_quotes_including_on_nested_default_elements()
        {
            const string Protected = "<p xml:space='preserve' a='one'><b xml:space='default' a='two'/></p>";
            var result = Format("<r>" + Protected + "<b a='three'/></r>", ("pnfmt_xml_quote_style", "double_quotes"));
            Assert.Contains(Protected, result);
            Assert.Contains("<b a=\"three\"/>", result);
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
