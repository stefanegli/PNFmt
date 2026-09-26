using System;
using System.Xml;
using System.Xml.Linq;

namespace PNFmt
{
    // Let the XML writer own markup, namespaces, encoding and layout. Protect
    // text newlines as character references so checkout conversion cannot alter
    // application data. Entitize on the inner writer already protects CRs.
    internal sealed class ResxXmlWriter : XmlWriter
    {
        private readonly XmlWriter writer;
        private readonly string layoutNewLine;
        private char[] buffer;
        private int depth;
        private bool inEntry;
        private XNode nextRootNode;

        public ResxXmlWriter(XmlWriter writer, string layoutNewLine, XElement root)
        {
            this.writer = writer;
            this.layoutNewLine = layoutNewLine;
            this.nextRootNode = root.FirstNode;
        }

        public override XmlWriterSettings Settings => this.writer.Settings;
        public override WriteState WriteState => this.writer.WriteState;
        public override string XmlLang => this.writer.XmlLang;
        public override XmlSpace XmlSpace => this.writer.XmlSpace;

        public override void WriteCData(string text)
        {
            if (text is not null && (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0))
            {
                // Character references are not interpreted inside CDATA. Escaped
                // text preserves its exact value while making newlines Git-safe.
                this.WriteString(text);
            }
            else
            {
                this.writer.WriteCData(text);
            }
        }

        public override void WriteComment(string text) => this.writer.WriteComment(this.Layout(text));

        public override void WriteProcessingInstruction(string name, string text) => this.writer.WriteProcessingInstruction(name, this.Layout(text));

        public override void WriteString(string text)
        {
            if (this.inEntry && this.depth == 2 && this.writer.WriteState != WriteState.Attribute && string.IsNullOrWhiteSpace(text))
            {
                // The entry's own indentation is layout, not its <value> or
                // <comment> content, even when xml:space is inherited from it.
                this.WriteWhitespace(text);
                return;
            }
            var next = text?.IndexOf('\n') ?? -1;
            if (next < 0 || this.writer.WriteState == WriteState.Attribute)
            {
                this.writer.WriteString(text);
                return;
            }

            this.buffer ??= new char[1024];
            var start = 0;
            while (next >= 0)
            {
                this.WriteSegment(text, start, next - start);
                this.writer.WriteCharEntity('\n');
                start = next + 1;
                next = text.IndexOf('\n', start);
            }
            this.WriteSegment(text, start, text.Length - start);
        }

        private string Layout(string text)
        {
            return this.layoutNewLine is null || text is null ? text
                : text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", this.layoutNewLine);
        }
        private void WriteSegment(string text, int start, int length)
        {
            while (length > 0)
            {
                var count = Math.Min(length, this.buffer.Length);
                // WriteChars requires surrogate pairs to be in the same call.
                if (count < length && char.IsHighSurrogate(text[start + count - 1]))
                {
                    count--;
                }
                text.CopyTo(start, this.buffer, 0, count);
                this.writer.WriteChars(this.buffer, 0, count);
                start += count;
                length -= count;
            }
        }
        // Whitespace here is XML layout; WriteWhitespace would entitize its CRs.
        public override void WriteWhitespace(string ws) => this.writer.WriteRaw(this.Layout(ws));
        public override void Close() => this.writer.Close();
        public override void Flush() => this.writer.Flush();
        public override string LookupPrefix(string ns) => this.writer.LookupPrefix(ns);
        public override void WriteBase64(byte[] bytes, int index, int count) => this.writer.WriteBase64(bytes, index, count);
        public override void WriteCharEntity(char ch) => this.writer.WriteCharEntity(ch);
        public override void WriteChars(char[] chars, int index, int count) => this.writer.WriteChars(chars, index, count);
        public override void WriteDocType(string name, string pubid, string sysid, string subset) => this.writer.WriteDocType(name, pubid, sysid, subset);
        public override void WriteEndAttribute() => this.writer.WriteEndAttribute();
        public override void WriteEndDocument() => this.writer.WriteEndDocument();
        public override void WriteEndElement()
        {
            this.writer.WriteEndElement();
            this.depth--;
        }
        public override void WriteEntityRef(string name) => this.writer.WriteEntityRef(name);
        public override void WriteFullEndElement()
        {
            this.writer.WriteFullEndElement();
            this.depth--;
        }
        public override void WriteRaw(char[] chars, int index, int count) => this.writer.WriteRaw(chars, index, count);
        public override void WriteRaw(string data) => this.writer.WriteRaw(data);
        public override void WriteStartAttribute(string prefix, string localName, string ns) => this.writer.WriteStartAttribute(prefix, localName, ns);
        public override void WriteStartDocument() => this.writer.WriteStartDocument();
        public override void WriteStartDocument(bool standalone) => this.writer.WriteStartDocument(standalone);
        public override void WriteStartElement(string prefix, string localName, string ns)
        {
            this.depth++;
            if (this.depth == 2)
            {
                while (!(this.nextRootNode is XElement))
                {
                    this.nextRootNode = this.nextRootNode.NextNode;
                }
                // RESX also permits a string directly in <data>, without a
                // <value> child. Its whitespace is data, not indentation.
                this.inEntry = ((XElement)this.nextRootNode).HasElements
                    && string.IsNullOrEmpty(ns) && (localName == "data" || localName == "metadata");
                this.nextRootNode = this.nextRootNode.NextNode;
            }
            this.writer.WriteStartElement(prefix, localName, ns);
        }
        public override void WriteSurrogateCharEntity(char lowChar, char highChar) => this.writer.WriteSurrogateCharEntity(lowChar, highChar);
    }
}
