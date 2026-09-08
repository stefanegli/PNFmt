using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PNFmt
{
    internal sealed class ResxLayoutSettings
    {
        public ResxLayoutSettings(IReadOnlyDictionary<string, string> settings)
        {
            var useTabs = false;
            if (settings.TryGetValue("indent_style", out var style)
                && (string.Equals(style, "tab", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(style, "space", StringComparison.OrdinalIgnoreCase)))
            {
                this.HasOverrides = true;
                useTabs = string.Equals(style, "tab", StringComparison.OrdinalIgnoreCase);
            }

            var width = 2;
            if (settings.TryGetValue("tab_width", out var tabWidth)
                && int.TryParse(tabWidth, out var parsedTabWidth) && parsedTabWidth > 0)
            {
                this.HasOverrides = true;
                width = parsedTabWidth;
            }

            if (settings.TryGetValue("indent_size", out var indentSize)
                && int.TryParse(indentSize, out var parsedIndentSize) && parsedIndentSize > 0)
            {
                this.HasOverrides = true;
                width = parsedIndentSize;
            }
            else if (string.Equals(indentSize, "tab", StringComparison.OrdinalIgnoreCase))
            {
                this.HasOverrides = true;
            }

            this.IndentChars = useTabs ? "\t" : new string(' ', width);

            if (settings.TryGetValue("end_of_line", out var endOfLine))
            {
                switch (endOfLine.ToLowerInvariant())
                {
                    case "lf": this.NewLine = "\n"; this.HasOverrides = true; break;
                    case "crlf": this.NewLine = "\r\n"; this.HasOverrides = true; break;
                    case "cr": this.NewLine = "\r"; this.HasOverrides = true; break;
                }
            }

            if (settings.TryGetValue("insert_final_newline", out var finalNewline)
                && bool.TryParse(finalNewline, out var parsedFinalNewline))
            {
                this.HasOverrides = true;
                this.InsertFinalNewline = parsedFinalNewline;
            }
        }

        public bool HasOverrides { get; }
        private string IndentChars { get; }
        private bool InsertFinalNewline { get; }
        private string NewLine { get; } = Environment.NewLine;

        public byte[] Serialize(XDocument document)
        {
            // xml:space on resource entries also preserves indentation around <value>.
            // Recreate that layout each time; leave the value's own whitespace untouched.
            foreach (var entry in document.Root.Elements()
                .Where(element => element.Name == "data" || element.Name == "metadata"))
            {
                var textNodes = entry.Nodes().Where(node => node.NodeType == XmlNodeType.Text).Cast<XText>().ToList();
                if (textNodes.All(node => string.IsNullOrWhiteSpace(node.Value)))
                {
                    foreach (var node in textNodes)
                    {
                        node.Remove();
                    }
                }
            }

            var writerSettings = new XmlWriterSettings
            {
                Encoding = Encoding.GetEncoding(document.Declaration?.Encoding ?? "utf-8"),
                Indent = true,
                IndentChars = this.IndentChars,
                NewLineChars = this.NewLine,
                // Preserve resource values, including carriage returns written as character references.
                NewLineHandling = NewLineHandling.Entitize,
            };
            using (var stream = new MemoryStream())
            {
                using (var writer = XmlWriter.Create(stream, writerSettings))
                {
                    document.Save(writer);
                }

                if (this.InsertFinalNewline)
                {
                    var newline = writerSettings.Encoding.GetBytes(this.NewLine);
                    stream.Write(newline, 0, newline.Length);
                }

                return stream.ToArray();
            }
        }
    }
}
