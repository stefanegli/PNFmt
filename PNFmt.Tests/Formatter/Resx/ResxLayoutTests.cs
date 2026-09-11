using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using PNFmt.Tests.Formatter.Resx.Fake;
using PNFmt.Tests.Formatter.Resx.TestFoundation;
using Xunit;

namespace PNFmt.Tests.Formatter.Resx
{
    public sealed class ResxLayoutTests
    {
        private const string Header = "<resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>";
        private const string EnableFormatting =
            "pnfmt_sort_entries = true\n"
            + "pnfmt_resx_remove_xsd_schema = true\n"
            + "pnfmt_resx_remove_documentation_comment = true\n";

        [Theory]
        [InlineData("end_of_line = lf\n")]
        [InlineData("end_of_line = crlf\n")]
        [InlineData("end_of_line = cr\n")]
        [InlineData("charset = utf-8\n")]
        [InlineData("charset = utf-16le\n")]
        public void Adding_generated_documentation_is_idempotent_with_layout_or_encoding_overrides(string settings)
        {
            using (var file = TemporaryFile.Create("<root>" + Header + "<data name='a'><value>1</value></data></root>"))
            {
                Configure(file, "pnfmt_sort_entries = true\n" + settings);
                var original = File.ReadAllBytes(file.Path);
                Assert.Equal(FileFormatStatus.Updated, Format(file, false).Status);
                Assert.Equal(original, File.ReadAllBytes(file.Path));
                Assert.Equal(FileFormatStatus.Updated, Format(file, true).Status);
                Assert.True(ResxDocumentFormatter.HasDocumentationComment(XDocument.Load(file.Path)));
                Assert.True(ResxDocumentFormatter.HasSchemaNode(XDocument.Load(file.Path)));
                var formatted = File.ReadAllBytes(file.Path);
                Assert.Equal(FileFormatStatus.Unchanged, Format(file, false).Status);
                Assert.Equal(FileFormatStatus.Unchanged, Format(file, true).Status);
                Assert.Equal(formatted, File.ReadAllBytes(file.Path));
            }
        }

        [Theory]
        [InlineData("lf", "\n", "true", true)]
        [InlineData("lf", "\n", "false", false)]
        [InlineData("lf", "\n", null, false)]
        [InlineData("crlf", "\r\n", "true", true)]
        [InlineData("crlf", "\r\n", "false", false)]
        [InlineData("crlf", "\r\n", null, false)]
        [InlineData("cr", "\r", "true", true)]
        [InlineData("cr", "\r", "false", false)]
        [InlineData("cr", "\r", null, false)]
        public void Layout_only_changes_are_detected_without_moving_comments(
            string endOfLine, string newline, string finalSetting, bool finalNewline)
        {
            var original = "<root>" + Header
                + "<data name=\"a\"><value>1</value></data><!-- keep between entries -->"
                + "<data name=\"b\"><value>2</value></data></root>\r\n\r\n";
            using (var file = TemporaryFile.Create(original))
            {
                Configure(file, EnableFormatting + "end_of_line = " + endOfLine + "\nindent_size = 4\ntab_width = 8\n"
                    + (finalSetting is null ? "" : "insert_final_newline = " + finalSetting + "\n"));
                var expected = string.Join(newline,
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
                    "<root>",
                    "    <resheader name=\"resmimetype\">",
                    "        <value>text/microsoft-resx</value>",
                    "    </resheader>",
                    "    <data name=\"a\">",
                    "        <value>1</value>",
                    "    </data>",
                    "    <!-- keep between entries -->",
                    "    <data name=\"b\">",
                    "        <value>2</value>",
                    "    </data>",
                    "</root>") + (finalNewline ? newline : "");

                AssertPreviewWriteAndStable(file, expected);
            }
        }

        [Theory]
        [InlineData("indent_style = tab\nindent_size = tab\ntab_width = 8\n", "\t")]
        [InlineData("tab_width = 3\n", "   ")]
        [InlineData("indent_size = 5\n", "     ")]
        public void Indentation_is_applied_alongside_sorting(string layout, string indent)
        {
            using (var file = TemporaryFile.Create("<root>" + Header
                + "<data name=\"z\"><value>2</value></data><data name=\"a\"><value>1</value></data></root>"))
            {
                Configure(file, EnableFormatting + layout + "end_of_line = lf\n");
                Assert.Equal(FileFormatStatus.Updated, Format(file, true).Status);
                var text = File.ReadAllText(file.Path);
                Assert.Contains("\n" + indent + "<resheader", text);
                Assert.Contains("\n" + indent + indent + "<value>1</value>", text);
                Assert.Equal(new[] { "a", "z" }, XDocument.Load(file.Path).Root.Elements("data").Select(e => (string)e.Attribute("name")));
                Assert.Equal(FileFormatStatus.Unchanged, Format(file, false).Status);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("end_of_line = lf\nindent_size = 4\n")]
        [InlineData("end_of_line = crlf\nindent_size = 4\n")]
        [InlineData("end_of_line = cr\nindent_size = 4\n")]
        [InlineData("charset = utf-16le\n")]
        public void Sorting_preserves_multiline_values_significant_whitespace_and_attributes(string settings)
        {
            var original = "<root>" + Header
                + "<data name=\"z\" xml:space=\"preserve\" marker=\"a&#xD;&#xA;&#x9;b\">"
                + "<value>  first\nsecond&#xD;third&#xD;&#xA;fourth\t  </value></data>"
                + "<data name=\"b\" xml:space=\"preserve\"><value>  \t\n </value></data></root>";
            using (var file = TemporaryFile.Create(original))
            {
                Configure(file, EnableFormatting + settings);
                var before = XDocument.Load(file.Path);
                var originalBytes = File.ReadAllBytes(file.Path);
                Assert.Equal(FileFormatStatus.Updated, Format(file, false).Status);
                Assert.Equal(originalBytes, File.ReadAllBytes(file.Path));
                Assert.Equal(FileFormatStatus.Updated, Format(file, true).Status);
                var after = XDocument.Load(file.Path);

                Assert.Equal(before.Root.Elements("data").OrderBy(e => (string)e.Attribute("name")).Select(e => e.Element("value").Value),
                    after.Root.Elements("data").Select(e => e.Element("value").Value));
                Assert.Equal((string)before.Root.Element("data").Attribute("marker"),
                    (string)after.Root.Elements("data").Last().Attribute("marker"));
                Assert.Equal(FileFormatStatus.Unchanged, Format(file, true).Status);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("indent_size = 4\nend_of_line = lf\n")]
        [InlineData("charset = utf-16le\n")]
        public void Whitespace_only_values_without_xml_space_survive_sorting(string settings)
        {
            var original = "<root>\n" + Header
                + "\n<data name=\"z\"><value>   </value><comment>\t </comment></data>"
                + "\n<data name=\"a\"><value>\t\n </value></data>\n</root>";
            using (var file = TemporaryFile.Create(original))
            {
                Configure(file, EnableFormatting + settings);
                var bytes = File.ReadAllBytes(file.Path);
                Assert.Equal(FileFormatStatus.Updated, Format(file, false).Status);
                Assert.Equal(bytes, File.ReadAllBytes(file.Path));
                Assert.Equal(FileFormatStatus.Updated, Format(file, true).Status);

                var entries = XDocument.Load(file.Path, LoadOptions.PreserveWhitespace).Root.Elements("data").ToArray();
                Assert.Equal(new[] { "a", "z" }, entries.Select(entry => (string)entry.Attribute("name")));
                Assert.Equal(new[] { "\t\n ", "   " }, entries.Select(entry => entry.Element("value").Value));
                Assert.Equal("\t ", entries[1].Element("comment").Value);
                var formatted = File.ReadAllBytes(file.Path);
                Assert.Equal(FileFormatStatus.Unchanged, Format(file, true).Status);
                Assert.Equal(formatted, File.ReadAllBytes(file.Path));
            }
        }

        [Fact]
        public void Final_newline_can_be_configured_without_other_layout_options()
        {
            using (var file = TemporaryFile.Create("<root>" + Header + "</root>"))
            {
                Configure(file, EnableFormatting + "insert_final_newline = true\n");
                Assert.Equal(FileFormatStatus.Updated, Format(file, true).Status);
                Assert.EndsWith("</root>" + Environment.NewLine, File.ReadAllText(file.Path));

                Configure(file, EnableFormatting + "insert_final_newline = false\n");
                Assert.Equal(FileFormatStatus.Updated, Format(file, true).Status);
                Assert.EndsWith("</root>", File.ReadAllText(file.Path));
                Assert.Equal(FileFormatStatus.Unchanged, Format(file, false).Status);
            }
        }

        [Fact]
        public void Layout_settings_alone_do_not_enable_resource_cleanup()
        {
            using (var file = TemporaryFile.Create("<root>" + Header + "</root>"))
            {
                Configure(file, "indent_size = 4\nend_of_line = lf\ninsert_final_newline = true\n");
                var original = File.ReadAllBytes(file.Path);
                Assert.Equal(FileFormatStatus.Skipped, Format(file, true).Status);
                Assert.Equal(original, File.ReadAllBytes(file.Path));
            }
        }

        [Fact]
        public void Layout_preserves_declared_utf16_encoding_and_remains_readable()
        {
            using (var file = TemporaryFile.Create(string.Empty))
            {
                File.WriteAllText(file.Path, "<?xml version=\"1.0\" encoding=\"utf-16\"?><root>" + Header + "</root>", Encoding.Unicode);
                Configure(file, EnableFormatting + "end_of_line = lf\ninsert_final_newline = true\n");
                Assert.Equal(FileFormatStatus.Updated, Format(file, true).Status);
                Assert.Equal("utf-16", XDocument.Load(file.Path).Declaration.Encoding);
                Assert.True(File.ReadAllBytes(file.Path).Take(2).SequenceEqual(Encoding.Unicode.GetPreamble()));
                Assert.Equal(FileFormatStatus.Unchanged, Format(file, false).Status);
            }
        }

        [Fact]
        public void Layout_inherits_parent_settings_and_child_final_newline_override()
        {
            using (var file = TemporaryFile.Create("<root>" + Header + "</root>"))
            {
                Configure(file, EnableFormatting + "end_of_line = lf\nindent_size = 3\ninsert_final_newline = true\n");
                var child = Path.Combine(file.DirectoryPath, "child");
                Directory.CreateDirectory(child);
                var path = Path.Combine(child, "Child.resx");
                File.Copy(file.Path, path);
                File.WriteAllText(Path.Combine(child, ".editorconfig"), "[*.resx]\ninsert_final_newline = false\n");
                var formatter = new ResxFormatter();

                Assert.Equal(FileFormatStatus.Updated, formatter.Format(new FileFormatRequest(path, true, false, new FakeLog())).Status);
                var formatted = File.ReadAllText(path);
                Assert.Contains("\n   <resheader", formatted);
                Assert.DoesNotContain("\r", formatted);
                Assert.EndsWith("</root>", formatted);
                Assert.Equal(FileFormatStatus.Unchanged, formatter.Format(new FileFormatRequest(path, false, false, new FakeLog())).Status);
            }
        }

        private static void AssertPreviewWriteAndStable(TemporaryFile file, string expected)
        {
            var original = File.ReadAllBytes(file.Path);
            Assert.Equal(FileFormatStatus.Updated, Format(file, false).Status);
            Assert.Equal(original, File.ReadAllBytes(file.Path));
            Assert.Equal(FileFormatStatus.Updated, Format(file, true).Status);
            var expectedBytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(expected)).ToArray();
            Assert.Equal(expectedBytes, File.ReadAllBytes(file.Path));
            Assert.Equal(FileFormatStatus.Unchanged, Format(file, false).Status);
            Assert.Equal(FileFormatStatus.Unchanged, Format(file, true).Status);
            Assert.Equal(expectedBytes, File.ReadAllBytes(file.Path));
        }

        private static FileFormatResult Format(TemporaryFile file, bool write)
        {
            return new ResxFormatter().Format(new FileFormatRequest(file.Path, write, false, new FakeLog()));
        }

        private static void Configure(TemporaryFile file, string properties)
        {
            File.WriteAllText(Path.Combine(file.DirectoryPath, ".editorconfig"), "root = true\n\n[*.resx]\n" + properties);
        }
    }
}
