using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

using LibGit2Sharp;

using Xunit;

namespace PNFmt.Tests.Formatter.Resx
{
    public sealed class ResxNewlineTests
    {
        private const string Configuration = "root = true\n[*.resx]\npnfmt_enabled = true\npnfmt_formatter = resx\n";
        private const string Resources = "<root><resheader name='resmimetype'><value>text/microsoft-resx</value></resheader>"
            + "<!-- first\r\nsecond --><?keep first\nsecond?>"
            + "<data name='Text' xml:space='preserve' marker='a&#xD;&#xA;&#x9;b'><value>  first\r\nsecond&#xD;third&#xD;&#xA;fourth\t\n\n  </value>"
            + "<comment>first\nsecond\rthird</comment></data>"
            + "<data name='CData'><value><![CDATA[first\nsecond\rthird\r\nfourth<&]]></value></data>"
            + "<data name='SingleCData'><value><![CDATA[plain<&]]></value></data>"
            + "<data name='CrCData'><value><![CDATA[left\rright]]></value></data>"
            + "<data name='Encoded'><value>first&#xA;second&#xD;third</value></data>"
            + "<data name='Whitespace' xml:space='preserve'><value> \t\r\n </value></data>"
            + "<data name='PlainWhitespace'><value> \t\r\n </value></data>"
            + "<data name='Direct'>first\r\nsecond</data><data name='DirectWhitespace'>\t\r\n</data>"
            + "<metadata name='Metadata'><value>first\nsecond&#xD;third</value></metadata></root>";
        private static readonly string[] ExpectedValues =
        {
            "  first\r\nsecond\rthird\r\nfourth\t\n\n  ",
            "first\nsecond\rthird\r\nfourth<&",
            "first\nsecond\rthird",
            " \t\r\n "
        };

        [Theory]
        [InlineData("lf")]
        [InlineData("crlf")]
        public async Task Compiled_resource_strings_survive_formatting_and_real_git_checkout(string endOfLine)
        {
            using var directory = new TestDirectory();
            directory.Write(".editorconfig", Configuration + "end_of_line = " + endOfLine + "\ncharset = utf-8\n");
            directory.Write(".gitattributes", "Before.resx -text\nAfter.resx text eol=" + endOfLine + "\n");
            directory.Write("Before.resx", Resources);
            var path = directory.Write("After.resx", Resources);
            Assert.Equal(FileFormatStatus.Updated, Format(path, true));
            var formatted = File.ReadAllBytes(path);

            Repository.Init(directory.Path);
            using (var repository = new Repository(directory.Path))
            {
                Commands.Stage(repository, new[] { ".gitattributes", "Before.resx", "After.resx" });
                var signature = new Signature("PNFmt Tests", "tests@example.invalid", DateTimeOffset.UtcNow);
                var commit = repository.Commit("Resource checkout fixture", signature, signature);
                File.Delete(path);
                repository.CheckoutPaths(commit.Sha, new[] { "After.resx" }, new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
            }
            Assert.Equal(formatted, File.ReadAllBytes(path));
            Assert.Equal(FileFormatStatus.Unchanged, Format(path, false));

            directory.Write("Probe.csproj", "<Project Sdk='Microsoft.NET.Sdk'><PropertyGroup><OutputType>Exe</OutputType>"
                + "<TargetFramework>net10.0</TargetFramework><RootNamespace>Probe</RootNamespace></PropertyGroup></Project>");
            directory.Write("Program.cs", "using System; using System.Reflection; using System.Resources; using System.Text.Json; "
                + "class Program { static void Main() { Console.WriteLine(JsonSerializer.Serialize(new[] { Read(\"Before\"), Read(\"After\") })); } "
                + "static string[] Read(string file) { var r = new ResourceManager(\"Probe.\" + file, Assembly.GetExecutingAssembly()); "
                + "return new[] { r.GetString(\"Text\"), r.GetString(\"CData\"), r.GetString(\"Encoded\"), r.GetString(\"Whitespace\"), "
                + "r.GetString(\"PlainWhitespace\"), r.GetString(\"Direct\"), r.GetString(\"DirectWhitespace\") }; } }");
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = directory.Path,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var argument in new[] { "run", "--project", "Probe.csproj", "--configuration", "Release", "--verbosity", "quiet" })
            {
                start.ArgumentList.Add(argument);
            }
            using var process = Process.Start(start);
            var result = await TestProcess.ReadAsync(process, TimeSpan.FromMinutes(2));
            Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
            var values = JsonSerializer.Deserialize<string[][]>(result.StandardOutput.Trim());
            Assert.Equal(ExpectedValues.Concat(new[] { "", "first\r\nsecond", "\t\r\n" }), values[0]);
            Assert.Equal(ExpectedValues.Concat(new[] { "", "first\r\nsecond", "\t\r\n" }), values[1]);
        }

        [Theory]
        [InlineData("pnfmt_sort_entries = true\npnfmt_format = false\n")]
        [InlineData("pnfmt_sort_entries = true\npnfmt_format = false\ncharset = utf-16le\n")]
        public void Content_only_rewrites_preserve_resource_newlines(string settings)
        {
            using var directory = new TestDirectory();
            directory.Write(".editorconfig", Configuration + settings);
            var path = directory.Write("Input.resx", Resources);
            Assert.Equal(FileFormatStatus.Updated, Format(path, true));
            AssertResourceValues(path);
            Assert.Equal(FileFormatStatus.Unchanged, Format(path, true));
            // Layout-disabled serialization retains physical XML comment newlines.
            Assert.Contains("<!-- first\r\nsecond -->", File.ReadAllText(path));
        }

        [Fact]
        public void Long_multiline_text_preserves_surrogate_pairs_and_xml_escaping()
        {
            using var directory = new TestDirectory();
            directory.Write(".editorconfig", Configuration + "end_of_line = crlf\n");
            var value = new string('x', 1023) + "\U0001F680" + new string('y', 1100) + "\n<&\r\n";
            var source = Resources.Replace("first&#xA;second&#xD;third", value.Replace("&", "&amp;").Replace("<", "&lt;"));
            var path = directory.Write("Input.resx", source);
            Assert.Equal(FileFormatStatus.Updated, Format(path, true));
            Assert.Equal(value, ReadResourceXml(path).Root.Elements("data").Single(e => (string)e.Attribute("name") == "Encoded").Element("value").Value);
            Assert.Equal(FileFormatStatus.Unchanged, Format(path, false));
        }

        [Theory]
        [InlineData("lf", "\n", "utf-8")]
        [InlineData("crlf", "\r\n", "utf-8")]
        [InlineData("cr", "\r", "utf-8")]
        [InlineData("crlf", "\r\n", "utf-8-bom")]
        [InlineData("crlf", "\r\n", "utf-16le")]
        [InlineData("crlf", "\r\n", "utf-16be")]
        [InlineData("crlf", "\r\n", "latin1")]
        [InlineData(null, null, null)]
        public void Multiline_resources_remain_stable_after_checkout_line_ending_conversion(
            string endOfLine, string newline, string charset)
        {
            using var directory = new TestDirectory();
            directory.Write(".editorconfig", Configuration
                + (endOfLine is null ? "" : "end_of_line = " + endOfLine + "\n")
                + (charset is null ? "" : "charset = " + charset + "\n"));
            var path = directory.Write("Input.resx", Resources);
            var original = File.ReadAllBytes(path);
            Assert.Equal(FileFormatStatus.Updated, Format(path, false));
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Equal(FileFormatStatus.Updated, Format(path, true));
            AssertResourceValues(path);

            var bytes = File.ReadAllBytes(path);
            var encoding = charset == "latin1" ? Encoding.Latin1
                : charset == "utf-16le" ? Encoding.Unicode
                : charset == "utf-16be" ? Encoding.BigEndianUnicode : Encoding.UTF8;
            var text = encoding.GetString(bytes);
            var checkoutText = text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", newline ?? Environment.NewLine);
            File.WriteAllBytes(path, encoding.GetBytes(checkoutText));
            Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.Equal(FileFormatStatus.Unchanged, Format(path, false));
            Assert.Equal(FileFormatStatus.Unchanged, Format(path, true));
            AssertResourceValues(path);
        }

        [Theory]
        [InlineData("<root><value>&#x0;</value></root>")]
        [InlineData("<root marker='&#x1;' />")]
        [InlineData("<root><value>&#xD800;&#xDC00;</value></root>")]
        [InlineData("<root><value>\0</value></root>")]
        [InlineData("<!DOCTYPE root [<!ENTITY x 'value'>]><root>&x;</root>")]
        public void Non_normalizing_resource_reader_still_rejects_invalid_xml(string source)
        {
            using var directory = new TestDirectory();
            directory.Write(".editorconfig", Configuration);
            var path = directory.Write("Input.resx", source);
            Assert.Throws<XmlException>(() => Format(path, true));
            Assert.Equal(source, File.ReadAllText(path));
        }

        [Theory]
        [InlineData("&#9;&#10;&#13;&#x20;&#xD7FF;&#xE000;&#xFFFD;&#x10000;&#x10ffff;", "\t\n\r \uD7FF\uE000\uFFFD\U00010000\U0010FFFF")]
        [InlineData("<![CDATA[&#0; &#xD800;]]>", "&#0; &#xD800;")]
        [InlineData("<![CDATA[&#999999999999999999999;]]>", "&#999999999999999999999;")]
        [InlineData("<![CDATA[&#x]]>", "&#x")]
        public void Valid_references_and_literal_reference_spellings_keep_their_values(string input, string expected)
        {
            using var directory = new TestDirectory();
            directory.Write(".editorconfig", Configuration + "end_of_line = crlf\n");
            var path = directory.Write("Input.resx", Resources.Replace("first&#xA;second&#xD;third", input));
            Assert.Equal(FileFormatStatus.Updated, Format(path, true));
            Assert.Equal(expected, ReadResourceXml(path).Root.Elements("data").Single(e => (string)e.Attribute("name") == "Encoded").Element("value").Value);
            Assert.Equal(FileFormatStatus.Unchanged, Format(path, false));
        }

        private static void AssertResourceValues(string path)
        {
            var root = ReadResourceXml(path).Root;
            var names = new[] { "Text", "CData", "Encoded", "Whitespace" };
            Assert.Equal(ExpectedValues, names.Select(name => root.Elements("data").Single(e => (string)e.Attribute("name") == name).Element("value").Value));
            Assert.Equal("first\nsecond\rthird", root.Elements("data").Single(e => (string)e.Attribute("name") == "Text").Element("comment").Value);
            Assert.Equal("first\nsecond\rthird", root.Element("metadata").Element("value").Value);
            Assert.Equal("a\r\n\tb", (string)root.Elements("data").Single(e => (string)e.Attribute("name") == "Text").Attribute("marker"));
            Assert.Equal("plain<&", root.Elements("data").Single(e => (string)e.Attribute("name") == "SingleCData").Element("value").Value);
            Assert.Equal("left\rright", root.Elements("data").Single(e => (string)e.Attribute("name") == "CrCData").Element("value").Value);
            Assert.Equal("first\r\nsecond", root.Elements("data").Single(e => (string)e.Attribute("name") == "Direct").Value);
            Assert.Equal("\t\r\n", root.Elements("data").Single(e => (string)e.Attribute("name") == "DirectWhitespace").Value);
        }

        private static FileFormatStatus Format(string path, bool write)
        {
            return new ResxFormatter().Format(new FileFormatRequest(path, write, false, NullFormatterLog.Instance)).Status;
        }

        private static XDocument ReadResourceXml(string path)
        {
            // Match the public ResXResourceReader/MSBuild contract, not the
            // normalizing XDocument.Load(path) overload that hid the regression.
            using var reader = new XmlTextReader(path) { Normalization = false, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }
    }
}
