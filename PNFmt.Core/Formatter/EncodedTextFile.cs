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

        private EncodedTextFile(string text, Encoding encoding)
        {
            this.Text = text;
            this.encoding = encoding;
        }

        public string Text { get; }

        public static EncodedTextFile Read(string path)
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
                        encoding.GetString(bytes, preamble.Length, bytes.Length - preamble.Length), encoding);
                }
            }

            // BOM-less files must be valid UTF-8. Do not guess legacy code pages or
            // replace undecodable bytes and then overwrite the user's original data.
            var utf8 = new UTF8Encoding(false, true);
            return new EncodedTextFile(utf8.GetString(bytes), utf8);
        }

        public void Write(string path, string text)
        {
            // Encode completely before opening the destination, so encoding failures
            // cannot truncate it. Keep precisely the original BOM convention.
            var bytes = this.encoding.GetPreamble().Concat(this.encoding.GetBytes(text)).ToArray();
            File.WriteAllBytes(path, bytes);
        }
    }
}
