// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PNFmt
{
    internal static class FileEncoding
    {
        private static readonly Regex Declaration = new Regex(@"\A<\?xml\s+[^?]*\?>", RegexOptions.CultureInvariant);
        private static readonly Regex EncodingAttribute = new Regex(@"\bencoding\s*=\s*(['""])(?<name>[^'""]+)\1", RegexOptions.CultureInvariant);
        private static readonly Regex VersionAttribute = new Regex(@"\bversion\s*=\s*(['""])[^'""]+\1", RegexOptions.CultureInvariant);

        public static Encoding Load(string path, IFormatterLog log)
        {
            var settings = EditorConfigSettings.Load(path, log);
            settings.TryGetValue("charset", out var charset);
            switch (charset?.ToLowerInvariant())
            {
                case "utf-8": return new UTF8Encoding(false, true);
                case "utf-8-bom": return new UTF8Encoding(true, true);
                case "utf-16le": return new UnicodeEncoding(false, true, true);
                case "utf-16be": return new UnicodeEncoding(true, true, true);
                case "latin1": return Encoding.GetEncoding(28591, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                default: return null;
            }
        }

        public static byte[] GetBytes(string text, Encoding encoding)
        {
            // Finish encoding before opening the destination. Never replace characters
            // that cannot be represented in the requested charset.
            return encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        }

        public static string UpdateXmlDeclaration(string text, Encoding encoding)
        {
            var declaration = Declaration.Match(text);
            if (!declaration.Success)
            {
                return encoding.CodePage == 65001 ? text
                    : "<?xml version=\"1.0\" encoding=\"" + encoding.WebName + "\"?>"
                        + TextFileFormatting.DetectNewLine(text) + text;
            }

            var attribute = EncodingAttribute.Match(declaration.Value);
            if (attribute.Success)
            {
                var name = attribute.Groups["name"];
                return text.Remove(name.Index, name.Length).Insert(name.Index, encoding.WebName);
            }

            // Encoding must precede standalone in an XML declaration.
            var version = VersionAttribute.Match(declaration.Value);
            return text.Insert(version.Index + version.Length, " encoding=\"" + encoding.WebName + "\"");
        }

        public static Encoding ReadXmlDeclaration(string text)
        {
            var declaration = Declaration.Match(text);
            var attribute = EncodingAttribute.Match(declaration.Value);
            return attribute.Success
                ? Encoding.GetEncoding(attribute.Groups["name"].Value, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
                : null;
        }
    }
}
