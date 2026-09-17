// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using Xunit;

namespace PNFmt.Tests.Formatter
{
    public sealed class CharsetTests
    {
        private const string ResxInput = "<?xml version=\"1.0\" encoding=\"utf-8\"?><root>"
            + "<resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>"
            + "<data name=\"Z\"><value>caf\u00e9 \u00c3\u00a9</value></data><data name=\"A\"><value>a</value></data></root>";

        public static IEnumerable<object[]> BomlessXmlCases =>
            from extension in new[] { "xml", "xaml", "slnx" }
            from inputEncoding in new[] { "utf-16", "utf-16BE", "utf-32", "utf-32BE" }
            from outputCharset in new[] { null, "utf-8" }
            select new object[] { extension, inputEncoding, outputCharset };

        public static IEnumerable<object[]> Cases =>
            from extension in new[] { "resx", "csproj", "cs", "xml", "xaml", "slnx", "ini", "editorconfig", "rsp" }
            from charset in new[] { "utf-8", "utf-8-bom", "utf-16le", "utf-16be", "latin1" }
            select new object[] { extension, charset };

        public static IEnumerable<object[]> EncodingOnlyXmlCases =>
            from extension in new[] { "csproj", "resx", "xml", "xaml", "slnx" }
            from inputEncoding in new[] { "iso-8859-1", "utf-16", "utf-16BE", "utf-32", "utf-32BE" }
            select new object[] { extension, inputEncoding };

        public static IEnumerable<object[]> FallbackCases =>
            from extension in new[] { "resx", "csproj", "cs", "xml", "xaml", "slnx", "ini", "editorconfig", "rsp" }
            from charset in new[] { null, "unset", "invalid" }
            select new object[] { extension, charset };

        public static IEnumerable<object[]> XmlFallbackCases => FallbackCases.Where(test => IsXml((string)test[0]));

        [Theory]
        [MemberData(nameof(FallbackCases))]
        public void Absent_unset_or_invalid_charset_preserves_existing_encoding_behavior(string extension, string charset)
        {
            using (var file = new TemporaryFile(extension, charset))
            {
                var original = File.ReadAllBytes(file.Path);
                Assert.Equal(FileFormatStatus.Updated, file.Run(false).Status);
                Assert.Equal(original, File.ReadAllBytes(file.Path));
                Assert.Equal(FileFormatStatus.Updated, file.Run(true).Status);
                var bytes = File.ReadAllBytes(file.Path);
                var expectBom = extension == "cs" || extension == "xml" || extension == "xaml" || extension == "resx";
                Assert.Equal(expectBom, bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
                Assert.Contains("caf\u00e9 \u00c3\u00a9", File.ReadAllText(file.Path));
                Assert.Equal(FileFormatStatus.Unchanged, file.Run(true).Status);
                Assert.Equal(bytes, File.ReadAllBytes(file.Path));
            }
        }

        [Theory]
        [MemberData(nameof(BomlessXmlCases))]
        public void Bomless_unicode_xml_is_decoded_before_output_charset_is_applied(string extension, string inputEncoding, string outputCharset)
        {
            using (var file = new TemporaryFile(extension, outputCharset))
            {
                var encoding = Encoding.GetEncoding(inputEncoding, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                var original = encoding.GetBytes(FileEncoding.UpdateXmlDeclaration(File.ReadAllText(file.Path), encoding));
                File.WriteAllBytes(file.Path, original);
                Assert.Contains("caf\u00e9 \u00c3\u00a9", XDocument.Load(file.Path).ToString());

                Assert.Equal(FileFormatStatus.Updated, file.Run(false).Status);
                Assert.Equal(original, File.ReadAllBytes(file.Path));
                Assert.Equal(FileFormatStatus.Updated, file.Run(true).Status);
                Assert.Contains("caf\u00e9 \u00c3\u00a9", XDocument.Load(file.Path).ToString());

                var formatted = File.ReadAllBytes(file.Path);
                if (outputCharset is null && extension != "slnx")
                {
                    Assert.Equal(original.Take(4), formatted.Take(4));
                    Assert.Equal(encoding.WebName, XDocument.Load(file.Path).Declaration.Encoding);
                }
                else
                {
                    Assert.Equal(new byte[] { 0x3C, 0x3F, 0x78 }, formatted.Take(3));
                    Assert.Equal("utf-8", XDocument.Load(file.Path).Declaration.Encoding);
                }

                Assert.Equal(FileFormatStatus.Unchanged, file.Run(true).Status);
                Assert.Equal(formatted, File.ReadAllBytes(file.Path));
            }
        }

        [Theory]
        [InlineData("resx")]
        [InlineData("csproj")]
        [InlineData("slnx")]
        [InlineData("xml")]
        [InlineData("xaml")]
        public void Bomless_utf8_xml_can_be_converted_to_latin1(string extension)
        {
            using (var file = new TemporaryFile(extension, "latin1"))
            {
                File.WriteAllText(file.Path, File.ReadAllText(file.Path), new UTF8Encoding(false));
                Assert.Equal(FileFormatStatus.Updated, file.Run(true).Status);
                Assert.Contains("caf\u00e9 \u00c3\u00a9", Encoding.GetEncoding(28591).GetString(File.ReadAllBytes(file.Path)));
                Assert.Equal("iso-8859-1", XDocument.Load(file.Path).Declaration.Encoding);
                Assert.Equal(FileFormatStatus.Unchanged, file.Run(true).Status);
            }
        }

        [Theory]
        [InlineData("resx")]
        [InlineData("csproj")]
        [InlineData("cs")]
        [InlineData("xml")]
        [InlineData("xaml")]
        [InlineData("slnx")]
        [InlineData("ini")]
        [InlineData("editorconfig")]
        [InlineData("rsp")]
        public void Charset_alone_does_not_activate_formatters(string extension)
        {
            using (var file = new TemporaryFile(extension, "utf-16be", enable: false))
            {
                var original = File.ReadAllBytes(file.Path);
                Assert.Equal(FileFormatStatus.Skipped, file.Run(true).Status);
                Assert.Equal(original, File.ReadAllBytes(file.Path));
            }
        }

        [Fact]
        public void Charset_is_inherited_and_child_override_is_case_insensitive()
        {
            using (var file = new TemporaryFile("resx", "utf-16be"))
            {
                var childDirectory = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(file.Path), "child");
                Directory.CreateDirectory(childDirectory);
                var childPath = System.IO.Path.Combine(childDirectory, "Child.resx");
                File.Copy(file.Path, childPath);
                var formatter = new ResxFormatter();
                Assert.Equal(FileFormatStatus.Updated, formatter.Format(new FileFormatRequest(childPath, true, false, file)).Status);
                Assert.Equal(new byte[] { 0xFE, 0xFF }, File.ReadAllBytes(childPath).Take(2));
                File.WriteAllText(System.IO.Path.Combine(childDirectory, ".editorconfig"), "[*.resx]\ncharset = UTF-8\n");
                Assert.Equal(FileFormatStatus.Updated, formatter.Format(new FileFormatRequest(childPath, true, false, file)).Status);
                Assert.Equal(new byte[] { 0x3C, 0x3F, 0x78 }, File.ReadAllBytes(childPath).Take(3));
                Assert.Contains("caf\u00e9", XDocument.Load(childPath).Root.Value);
                Assert.Equal(FileFormatStatus.Unchanged, formatter.Format(new FileFormatRequest(childPath, true, false, file)).Status);
            }
        }

        [Theory]
        [InlineData("xml")]
        [InlineData("xaml")]
        public void Declared_utf8_without_a_bom_retains_its_bom_convention(string extension)
        {
            using (var file = new TemporaryFile(extension, null))
            {
                File.WriteAllText(file.Path, "<?xml version='1.0' encoding='utf-8'?>" + File.ReadAllText(file.Path), new UTF8Encoding(false));
                Assert.Equal(FileFormatStatus.Updated, file.Run(true).Status);
                Assert.False(File.ReadAllBytes(file.Path).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
                Assert.Equal(FileFormatStatus.Unchanged, file.Run(true).Status);
            }
        }

        [Theory]
        [InlineData("xml")]
        [InlineData("xaml")]
        public void Encoding_is_inserted_before_standalone_and_legacy_xml_remains_readable(string extension)
        {
            using (var file = new TemporaryFile(extension, "latin1"))
            {
                File.WriteAllText(file.Path, "<?xml version='1.0' standalone='yes'?><root>caf\u00e9</root>", new UTF8Encoding(true));
                Assert.Equal(FileFormatStatus.Updated, file.Run(true).Status);
                Assert.Equal("caf\u00e9", XDocument.Load(file.Path).Root.Value);
                Assert.Equal(FileFormatStatus.Unchanged, file.Run(true).Status);
            }
        }

        [Theory]
        [MemberData(nameof(EncodingOnlyXmlCases))]
        public void Encoding_only_xml_changes_preserve_declared_input_text(string extension, string inputEncoding)
        {
            var settings = "pnfmt_enabled = true\npnfmt_formatter = " + extension + "\npnfmt_format = false\n";
            using (var file = new TemporaryFile(extension, "utf-8", settings, enable: false))
            {
                var encoding = Encoding.GetEncoding(inputEncoding, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                var source = FileEncoding.UpdateXmlDeclaration(File.ReadAllText(file.Path), encoding);
                var original = encoding.GetBytes(source);
                File.WriteAllBytes(file.Path, original);
                var expected = new UTF8Encoding(false, true).GetBytes(source.Replace(encoding.WebName, "utf-8"));

                Assert.Equal(FileFormatStatus.Updated, file.Run(false).Status);
                Assert.Equal(original, File.ReadAllBytes(file.Path));
                Assert.Equal(FileFormatStatus.Updated, file.Run(true).Status);
                Assert.Equal(expected, File.ReadAllBytes(file.Path));
                Assert.Contains("caf\u00e9 \u00c3\u00a9", XDocument.Load(file.Path).ToString());
                Assert.Equal(FileFormatStatus.Unchanged, file.Run(true).Status);
                Assert.Equal(expected, File.ReadAllBytes(file.Path));
            }
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void Every_formatter_honors_charset_in_preview_write_and_encoding_only_changes(string extension, string charset)
        {
            using (var file = new TemporaryFile(extension, charset))
            {
                var original = File.ReadAllBytes(file.Path);
                Assert.Equal(FileFormatStatus.Updated, file.Run(false).Status);
                Assert.Equal(original, File.ReadAllBytes(file.Path));
                Assert.Equal(FileFormatStatus.Updated, file.Run(true).Status);
                var formatted = File.ReadAllBytes(file.Path);
                var encoding = ExpectedEncoding(charset);
                var text = encoding.GetString(formatted, encoding.GetPreamble().Length, formatted.Length - encoding.GetPreamble().Length);
                Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(text)), formatted);
                Assert.False(text.StartsWith("\uFEFF", StringComparison.Ordinal));
                Assert.Contains("caf\u00e9 \u00c3\u00a9", text);
                if (IsXml(extension))
                {
                    var document = XDocument.Load(file.Path);
                    Assert.Equal(encoding.WebName, document.Declaration?.Encoding ?? "utf-8");
                }

                Assert.Equal(FileFormatStatus.Unchanged, file.Run(true).Status);
                Assert.Equal(formatted, File.ReadAllBytes(file.Path));

                // Change only the physical encoding, leaving the formatted text intact.
                // XML declarations must describe those input bytes so XML readers can load them.
                var otherEncoding = charset == "utf-8-bom" ? new UTF8Encoding(false) : new UTF8Encoding(true);
                if (IsXml(extension))
                {
                    text = FileEncoding.UpdateXmlDeclaration(text, otherEncoding);
                }

                File.WriteAllText(file.Path, text, otherEncoding);
                var wrongEncoding = File.ReadAllBytes(file.Path);
                Assert.Equal(FileFormatStatus.Updated, file.Run(false).Status);
                Assert.Equal(wrongEncoding, File.ReadAllBytes(file.Path));
                Assert.Equal(FileFormatStatus.Updated, file.Run(true).Status);
                Assert.Equal(formatted, File.ReadAllBytes(file.Path));
                Assert.Equal(FileFormatStatus.Unchanged, file.Run(false).Status);
            }
        }

        [Fact]
        public void Generated_csharp_does_not_get_encoding_changes()
        {
            using (var file = new TemporaryFile("cs", "utf-16be", "generated_code = true\n"))
            {
                var original = File.ReadAllBytes(file.Path);
                Assert.Equal(FileFormatStatus.Skipped, file.Run(true).Status);
                Assert.Equal(original, File.ReadAllBytes(file.Path));
            }
        }

        [Theory]
        [MemberData(nameof(FallbackCases))]
        public void Invalid_utf8_is_rejected_without_replacing_characters(string extension, string charset)
        {
            using (var file = new TemporaryFile(extension, charset))
            {
                var invalid = Encoding.Latin1.GetBytes(File.ReadAllText(file.Path));
                File.WriteAllBytes(file.Path, invalid);
                foreach (var write in new[] { false, true })
                {
                    var exception = Record.Exception(() => file.Run(write));
                    Assert.True(exception is DecoderFallbackException || exception is System.Xml.XmlException,
                        "Expected an encoding error, got: " + exception);
                    Assert.Equal(invalid, File.ReadAllBytes(file.Path));
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Resx_utf8_does_not_add_a_bom_with_or_without_layout_overrides(bool layout)
        {
            using (var file = new TemporaryFile("resx", "utf-8", layout ? "indent_size = 4\n" : ""))
            {
                File.WriteAllText(file.Path, ResxInput, new UTF8Encoding(false));
                Assert.Equal(FileFormatStatus.Updated, file.Run(true).Status);
                Assert.Equal(new byte[] { 0x3C, 0x3F, 0x78 }, File.ReadAllBytes(file.Path).Take(3));
                Assert.Equal(FileFormatStatus.Unchanged, file.Run(true).Status);
            }
        }

        [Theory]
        [InlineData("cs")]
        [InlineData("xml")]
        [InlineData("xaml")]
        public void Skipped_or_invalid_files_do_not_get_encoding_changes(string extension)
        {
            using (var file = new TemporaryFile(extension, "utf-16be"))
            {
                File.WriteAllText(file.Path, extension == "cs" ? "class C{" : "<root>", new UTF8Encoding(false));
                var original = File.ReadAllBytes(file.Path);
                Assert.Equal(FileFormatStatus.Skipped, file.Run(true).Status);
                Assert.Equal(original, File.ReadAllBytes(file.Path));
            }

        }

        [Fact]
        public void Unrepresentable_text_is_rejected_before_writing()
        {
            using (var file = new TemporaryFile("cs", "latin1"))
            {
                File.WriteAllText(file.Path, "class C{string s=\"\u65e5\";}", new UTF8Encoding(true));
                var original = File.ReadAllBytes(file.Path);
                Assert.Throws<EncoderFallbackException>(() => file.Run(false));
                Assert.Throws<EncoderFallbackException>(() => file.Run(true));
                Assert.Equal(original, File.ReadAllBytes(file.Path));
            }
        }

        [Theory]
        [MemberData(nameof(XmlFallbackCases))]
        public void Xml_declarations_identify_input_encoding_without_a_charset(string extension, string charset)
        {
            using (var file = new TemporaryFile(extension, charset))
            {
                var text = FileEncoding.UpdateXmlDeclaration(File.ReadAllText(file.Path), Encoding.Latin1);
                var original = Encoding.Latin1.GetBytes(text);
                File.WriteAllBytes(file.Path, original);
                Assert.Equal(FileFormatStatus.Updated, file.Run(false).Status);
                Assert.Equal(original, File.ReadAllBytes(file.Path));
                Assert.Equal(FileFormatStatus.Updated, file.Run(true).Status);
                Assert.Contains("caf\u00e9 \u00c3\u00a9", XDocument.Load(file.Path).ToString());
                var formatted = File.ReadAllBytes(file.Path);
                Assert.Equal(FileFormatStatus.Unchanged, file.Run(true).Status);
                Assert.Equal(formatted, File.ReadAllBytes(file.Path));
            }
        }

        private static Encoding ExpectedEncoding(string charset)
        {
            switch (charset)
            {
                case "utf-8": return new UTF8Encoding(false, true);
                case "utf-8-bom": return new UTF8Encoding(true, true);
                case "utf-16le": return new UnicodeEncoding(false, true, true);
                case "utf-16be": return new UnicodeEncoding(true, true, true);
                case "latin1": return Encoding.GetEncoding(28591);
                default: throw new ArgumentException(nameof(charset));
            }
        }

        private static bool IsXml(string extension) =>
            new[] { "resx", "csproj", "slnx", "xml", "xaml" }.Contains(extension);

        private sealed class TemporaryFile : IDisposable, IFormatterLog
        {
            private readonly string directory;
            private readonly IFileFormatter formatter;

            public TemporaryFile(string extension, string charset, string extraSettings = "", bool enable = true)
            {
                this.directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PNFmtCharsetTests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(this.directory);
                this.Path = System.IO.Path.Combine(this.directory, "Test." + extension);
                var settings = "root = true\n[*]\n" + (charset is null ? "" : "charset = " + charset + "\n") + extraSettings;
                if (enable)
                {
                    settings += "pnfmt_sort_entries = true\npnfmt_csharp_format = true\npnfmt_xml_format = true\npnfmt_xaml_format = true\n"
                        + "pnfmt_resx_remove_xsd_schema = true\npnfmt_resx_remove_documentation_comment = true\n";
                }

                File.WriteAllText(System.IO.Path.Combine(this.directory, ".editorconfig"), settings);
                string text;
                switch (extension)
                {
                    case "resx": this.formatter = new ResxFormatter(); text = ResxInput; break;
                    case "csproj": this.formatter = new CsProjFormatter(); text = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><Description>caf\u00e9 \u00c3\u00a9</Description></PropertyGroup></Project>"; break;
                    case "cs": this.formatter = new CSharpFormatter(); text = "class C{string s=\"caf\u00e9 \u00c3\u00a9\";}"; break;
                    case "xml": this.formatter = new XmlFormatter(); text = "<root><child>caf\u00e9 \u00c3\u00a9</child></root>"; break;
                    case "xaml": this.formatter = new XamlFormatter(); text = "<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><TextBlock Text=\"caf\u00e9 \u00c3\u00a9\"/></Grid>"; break;
                    case "slnx": this.formatter = new SlnxFormatter(); text = "<Solution><Project Path=\"caf\u00e9 \u00c3\u00a9.csproj\"/></Solution>"; break;
                    case "rsp": this.formatter = new RspFormatter(); text = "z\ncaf\u00e9 \u00c3\u00a9\na\n"; break;
                    default: this.formatter = new IniFormatter(); text = "[*]\nz=2\na=caf\u00e9 \u00c3\u00a9\n"; break;
                }

                File.WriteAllText(this.Path, text, new UTF8Encoding(true));
            }

            public string Path { get; }
            public void Dispose() => Directory.Delete(this.directory, true);
            public FileFormatResult Run(bool write) => this.formatter.Format(new FileFormatRequest(this.Path, write, false, this));
            public void Write(Exception exception) => throw new InvalidOperationException("Unexpected formatter error.", exception);
            public void WriteLine(string message) { }
        }
    }
}
