// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using Xunit;

namespace PNFmt.Tests.Formatter.Xml
{
    public sealed class XmlFormatterTests
    {
        [Fact]
        public void Attribute_values_comments_cdata_and_prefixes_keep_their_exact_spelling()
        {
            const string Child = "<p:child xmlns:p='urn:p' value='one\r\n  two &quot; &#x41;' second=\"&apos;\" />";
            const string Comment = "<!-- Keep\r\n   this comment -->";
            var result = Format("<root>" + Child + Comment + "<data><![CDATA[ < > & ]]></data></root>", ("end_of_line", "lf"));
            Assert.Contains(Child, result);
            Assert.Contains(Comment, result);
            Assert.Contains("<data><![CDATA[ < > & ]]></data>", result);
        }

        [Fact]
        public void Excessive_nesting_is_rejected_before_recursive_rendering()
        {
            var input = string.Concat(Enumerable.Repeat("<n>", 257)) + string.Concat(Enumerable.Repeat("</n>", 257));
            Assert.Throws<XmlException>(() => Format(input));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void File_formatter_is_opt_in_and_preserves_encoding_in_preview_and_write_modes(bool xaml)
        {
            var encodings = new Encoding[] { new UTF8Encoding(false), new UTF8Encoding(true), new UnicodeEncoding(false, true),
                new UnicodeEncoding(true, true), new UTF32Encoding(false, true), new UTF32Encoding(true, true) };
            foreach (var encoding in encodings)
            {
                using (var file = new TemporaryMarkup(xaml))
                {
                    const string Input = "<?xml version='1.0'?><Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Child Text='Grüezi 日本語 😀'/></Grid>";
                    File.WriteAllText(file.Path, Input, encoding);
                    var original = File.ReadAllBytes(file.Path);
                    Assert.Equal(FileFormatStatus.Skipped, file.Run(true).Status);
                    file.Enable();
                    Assert.Equal(FileFormatStatus.Updated, file.Run(false).Status);
                    Assert.Equal(original, File.ReadAllBytes(file.Path));
                    Assert.Equal(FileFormatStatus.Updated, file.Run(true).Status);
                    var expected = XmlDocumentFormatter.Format(Input, new Dictionary<string, string>(), xaml);
                    Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(expected)), File.ReadAllBytes(file.Path));
                    Assert.Equal(FileFormatStatus.Unchanged, file.Run(true).Status);
                }
            }
        }

        [Fact]
        public void Honors_tabs_newlines_and_optional_final_newline()
        {
            Assert.Equal("<root>\r\n\t<child>\r\n\t\t<leaf/>\r\n\t</child>\r\n</root>\r\n",
                Format("<root><child><leaf/></child></root>", ("indent_style", "tab"),
                    ("end_of_line", "crlf"), ("insert_final_newline", "true")));
            Assert.Equal("<root/>\r\n", Format("<root/>\r\n"));
            Assert.Equal("<root/>", Format("<root/>"));
        }

        [Fact]
        public void Indents_elements_without_reordering_or_reserializing_markup()
        {
            const string Input = "<?xml version='1.0' encoding='utf-8'?><root z='2' a=\"1\"><z/><a value='a &amp; b > c'/><!-- note --><?instruction value?></root>";
            var result = Format(Input, ("indent_size", "2"));

            Assert.Equal("<?xml version='1.0' encoding='utf-8'?>\n<root z='2' a=\"1\">\n  <z/>\n  <a value='a &amp; b > c'/>\n  <!-- note -->\n  <?instruction value?>\n</root>", result);
            Assert.True(XNode.DeepEquals(XDocument.Parse(Input), XDocument.Parse(result)));
            Assert.Equal(result, Format(result, ("indent_size", "2")));
        }

        [Theory]
        [InlineData("<root>")]
        [InlineData("<root a='1' a='2'/>")]
        [InlineData("<root/><other/>")]
        [InlineData("<p:root/>")]
        [InlineData("<!DOCTYPE root [<!ENTITY value 'content'>]><root>&value;</root>")]
        [InlineData("<!DOCTYPE root SYSTEM 'file:///nonexistent.dtd'><root/>")]
        public void Invalid_xml_and_doctypes_are_rejected(string text)
        {
            Assert.Throws<XmlException>(() => Format(text));
        }

        [Theory]
        [InlineData(false, "XML001")]
        [InlineData(true, "XAML001")]
        public void Malformed_files_are_skipped_with_a_diagnostic_without_writing(bool xaml, string code)
        {
            using (var file = new TemporaryMarkup(xaml))
            {
                file.Enable();
                File.WriteAllText(file.Path, "<root>");
                var result = file.Run(true);
                Assert.Equal(FileFormatStatus.Skipped, result.Status);
                Assert.Equal(code, Assert.Single(result.Diagnostics).Code);
                Assert.Equal("<root>", File.ReadAllText(file.Path));
            }
        }

        [Theory]
        [InlineData("<p>Hello <b>world</b> !</p>")]
        [InlineData("<p><b>one</b> text <i>two</i></p>")]
        [InlineData("<p>&#32;<b>space</b></p>")]
        [InlineData("<p><![CDATA[one\n  two]]><b>three</b></p>")]
        [InlineData("<p>  </p>")]
        [InlineData("<p></p>")]
        [InlineData("<p>&#xA0;</p>")]
        public void Preserves_text_and_mixed_content_exactly(string content)
        {
            var result = Format("<root>" + content + "<other/></root>");
            Assert.Contains(content, result);
            Assert.Equal(result, Format(result));
        }

        [Theory]
        [InlineData(false, "false")]
        [InlineData(false, "invalid")]
        [InlineData(true, "false")]
        [InlineData(true, "invalid")]
        public void Shared_sort_setting_and_non_true_activation_do_not_enable_formatting(bool xaml, string setting)
        {
            using (var file = new TemporaryMarkup(xaml))
            {
                const string Input = "<root><child/></root>";
                File.WriteAllText(file.Path, Input);
                file.Enable(setting);
                Assert.Equal(FileFormatStatus.Skipped, file.Run(true).Status);
                Assert.Equal(Input, File.ReadAllText(file.Path));
            }
        }

        [Fact]
        public void Xml_space_preserves_the_entire_subtree_even_with_a_nested_default()
        {
            const string Protected = "<p xml:space='preserve'> \r\n <b xml:space='default'><a/><z/></b> </p>";
            var result = Format("<root>" + Protected + "<other/></root>", ("end_of_line", "lf"));
            Assert.Contains(Protected, result);
            Assert.Contains("\n    <other/>\n", result);
        }

        private static string Format(string text, params (string Key, string Value)[] settings)
        {
            return XmlDocumentFormatter.Format(text, settings.ToDictionary(item => item.Key, item => item.Value));
        }

        private sealed class TemporaryMarkup : IDisposable
        {
            private readonly TestDirectory directory = new TestDirectory();
            private readonly bool xaml;

            public TemporaryMarkup(bool xaml)
            {
                this.xaml = xaml;
                this.directory.Write(".editorconfig", "root = true\n");
            }

            public string Path => this.directory.GetPath(this.xaml ? "View.xaml" : "Data.xml");

            public void Dispose() => this.directory.Dispose();

            public void Enable(string value = "true")
            {
                this.directory.Write(".editorconfig", (this.xaml
                    ? "root = true\n[*.xaml]\npnfmt_xaml_format = "
                    : "root = true\n[*.xml]\npnfmt_xml_format = ") + value + "\npnfmt_sort_entries = true\n");
            }

            public FileFormatResult Run(bool write)
            {
                IFileFormatter formatter = this.xaml ? new XamlFormatter() : new XmlFormatter();
                return formatter.Format(new FileFormatRequest(this.Path, write, false, NullFormatterLog.Instance));
            }
        }
    }
}
