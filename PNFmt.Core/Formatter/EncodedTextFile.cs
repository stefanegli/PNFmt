// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using System.Linq;
using System.Text;

namespace PNFmt
{
    internal sealed class EncodedTextFile
    {
        private readonly Encoding encoding;
        private readonly byte[] preamble;

        private EncodedTextFile(string text, Encoding encoding, byte[] preamble = null)
        {
            this.Text = text;
            this.encoding = encoding;
            this.preamble = preamble ?? Array.Empty<byte>();
        }

        public string Text { get; }

        public static EncodedTextFile Read(string path, Encoding configuredEncoding = null, bool xml = false)
        {
            var bytes = File.ReadAllBytes(path);
            // Check UTF-32 before UTF-16: their little-endian BOMs share a prefix.
            var encodings = new Encoding[]
            {
                new UTF32Encoding(false, true, true),
                new UTF32Encoding(true, true, true),
                new UTF8Encoding(true, true),
                new UnicodeEncoding(false, true, true),
                new UnicodeEncoding(true, true, true),
            };
            foreach (var encoding in encodings)
            {
                var preamble = encoding.GetPreamble();
                if (bytes.Take(preamble.Length).SequenceEqual(preamble))
                {
                    return new EncodedTextFile(
                        encoding.GetString(bytes, preamble.Length, bytes.Length - preamble.Length), encoding, preamble);
                }
            }

            if (xml)
            {
                // XML's opening byte signature identifies Unicode byte order even
                // without a BOM; ASCII cannot read a UTF-16/32 declaration.
                var signatureEncoding = DetectXmlUnicodeEncoding(bytes);
                if (signatureEncoding is not null)
                {
                    return new EncodedTextFile(signatureEncoding.GetString(bytes), signatureEncoding);
                }

                // The declaration describes the input, independently of the requested output.
                var declarationEncoding = FileEncoding.ReadXmlDeclaration(
                    Encoding.ASCII.GetString(bytes));
                if (declarationEncoding is not null)
                {
                    return new EncodedTextFile(declarationEncoding.GetString(bytes), declarationEncoding);
                }
            }

            // For non-XML text without a BOM, an explicit Latin-1 setting describes
            // the input too. XML without a declaration defaults to UTF-8 independently.
            // Guessing UTF-8 for Latin-1 text can corrupt it on a second run.
            if (!xml && configuredEncoding?.CodePage == 28591)
            {
                return new EncodedTextFile(configuredEncoding.GetString(bytes), configuredEncoding);
            }

            // Otherwise require valid UTF-8; never replace undecodable input bytes.
            var utf8 = new UTF8Encoding(false, true);
            return new EncodedTextFile(utf8.GetString(bytes), utf8);
        }

        public void Write(string path, string text)
        {
            // Encode completely before opening the destination, so encoding failures
            // cannot truncate it. Keep precisely the original BOM convention.
            var bytes = this.preamble.Concat(this.encoding.GetBytes(text)).ToArray();
            File.WriteAllBytes(path, bytes);
        }

        private static Encoding DetectXmlUnicodeEncoding(byte[] bytes)
        {
            if (bytes.Length < 4)
            {
                return null;
            }

            if (bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0x00 && bytes[3] == 0x3C)
            {
                return new UTF32Encoding(true, false, true);
            }

            if (bytes[0] == 0x3C && bytes[1] == 0x00 && bytes[2] == 0x00 && bytes[3] == 0x00)
            {
                return new UTF32Encoding(false, false, true);
            }

            if (bytes[0] == 0x00 && bytes[1] == 0x3C && bytes[2] == 0x00 && bytes[3] == 0x3F)
            {
                return new UnicodeEncoding(true, false, true);
            }

            if (bytes[0] == 0x3C && bytes[1] == 0x00 && bytes[2] == 0x3F && bytes[3] == 0x00)
            {
                return new UnicodeEncoding(false, false, true);
            }

            return null;
        }
    }
}
