using System.IO;
using System.Text;

using Xunit;

namespace PNFmt.Tests.Formatter
{
    public sealed class EncodedTextFileTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("<")]
        [InlineData("<?")]
        [InlineData("<?x")]
        [InlineData("<root/>")]
        public void Short_or_unrecognized_xml_signatures_preserve_utf8_input(string text)
        {
            using var directory = new TestDirectory();
            var path = directory.Write("Input.xml", text);
            var file = EncodedTextFile.Read(path, xml: true);
            Assert.Equal(text, file.Text);
            Assert.Equal(Encoding.UTF8.GetBytes(text), file.GetBytes(text));
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void Xml_unicode_without_a_bom_round_trips_and_rejects_incomplete_code_units(bool utf32, bool bigEndian)
        {
            Encoding encoding = utf32 ? new UTF32Encoding(bigEndian, false, true) : new UnicodeEncoding(bigEndian, false, true);
            var text = $"<?xml version='1.0' encoding='{encoding.WebName}'?><root>caf\u00e9 \u65e5</root>";
            var original = encoding.GetBytes(text);
            using var directory = new TestDirectory();
            var path = directory.GetPath("Input.xml");
            File.WriteAllBytes(path, original);

            var file = EncodedTextFile.Read(path, xml: true);
            Assert.Equal(text, file.Text);
            Assert.Equal(original, file.GetBytes(file.Text));
            Assert.Equal(original, File.ReadAllBytes(path));

            File.WriteAllBytes(path, original[..^1]);
            Assert.Throws<DecoderFallbackException>(() => EncodedTextFile.Read(path, xml: true));
        }
    }
}
