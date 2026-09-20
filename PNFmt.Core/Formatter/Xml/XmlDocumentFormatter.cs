// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace PNFmt
{
    internal static class XmlDocumentFormatter
    {
        public static string Format(string text, IReadOnlyDictionary<string, string> settings, bool xaml = false, Encoding outputEncoding = null)
        {
            var elements = ReadElements(text, xaml);
            var document = ParseLayout(text, elements);
            settings.TryGetValue("end_of_line", out var endOfLine);
            var newLine = endOfLine == "lf" ? "\n" : endOfLine == "crlf" ? "\r\n" : endOfLine == "cr" ? "\r"
                : TextFileFormatting.DetectNewLine(text);
            settings.TryGetValue("indent_style", out var indentStyle);
            settings.TryGetValue("indent_size", out var indentSize);
            if (indentSize == "tab")
            {
                settings.TryGetValue("tab_width", out indentSize);
            }

            var width = int.TryParse(indentSize, out var size) && size > 0 && size <= 256 ? size : 4;
            var indent = indentStyle == "tab" ? "\t" : new string(' ', width);
            var output = new StringBuilder(text.Length);
            foreach (var child in document.Children.Where(node => !node.IsWhitespace))
            {
                if (output.Length > 0)
                {
                    output.Append(newLine);
                }

                Render(child, text, output, 0, indent, newLine);
            }

            if (EditorConfigSettings.IsEnabled(settings, "insert_final_newline")
                || text.EndsWith("\n", StringComparison.Ordinal) || text.EndsWith("\r", StringComparison.Ordinal))
            {
                output.Append(newLine);
            }

            var result = WrapAttributes(output.ToString(), settings, indent, newLine, width, xaml, outputEncoding);
            ReadElements(result, xaml);
            return result;
        }

        public static string WrapAttributes(string text, IReadOnlyDictionary<string, string> settings,
            string indent, string newLine, int tabWidth, bool xaml = false, Encoding outputEncoding = null)
        {
            var wrapper = new XmlAttributeWrapper(settings, indent, newLine, tabWidth);
            if (!wrapper.Enabled)
            {
                return text;
            }

            // The file pipeline also updates declarations during encoding. Do it
            // before measuring so a new or longer encoding attribute is wrapped
            // on this pass, rather than causing another change on the next run.
            if (outputEncoding is not null)
            {
                text = FileEncoding.UpdateXmlDeclaration(text, outputEncoding);
            }

            var document = ParseLayout(text, ReadElements(text, xaml, enforceDepthLimit: false));
            var pending = new Stack<(Node Node, int Depth)>();
            foreach (var child in document.Children.AsEnumerable().Reverse())
            {
                pending.Push((child, 0));
            }

            var copied = 0;
            while (pending.Count > 0)
            {
                var (node, depth) = pending.Pop();
                wrapper.Append(text, copied, node.Start - copied);
                if (node.Preserve || (node.Children.Count > 0 && node.Children.All(child => child.IsWhitespace)))
                {
                    wrapper.Append(text, node.Start, node.End - node.Start);
                    copied = node.End;
                    continue;
                }

                if (node.IsElement)
                {
                    wrapper.AppendTag(text, node.Start, node.OpenEnd, depth);
                    copied = node.OpenEnd;
                    foreach (var child in node.Children.AsEnumerable().Reverse())
                    {
                        pending.Push((child, depth + 1));
                    }
                }
                else
                {
                    // Ordinary PI data is opaque text. Only the XML declaration
                    // has attributes whose separating whitespace can be changed.
                    if (StartsWith(text, node.Start, "<?xml") && node.End - node.Start > 5
                        && IsXmlWhitespace(text, node.Start + 5, node.Start + 6))
                    {
                        wrapper.AppendTag(text, node.Start, node.End, depth, declaration: true);
                    }
                    else
                    {
                        wrapper.Append(text, node.Start, node.End - node.Start);
                    }
                    copied = node.End;
                }
            }
            wrapper.Append(text, copied, text.Length - copied);
            return wrapper.ToString();
        }

        private static void AppendIndent(StringBuilder output, int depth, string indent)
        {
            for (var index = 0; index < depth; index++)
            {
                output.Append(indent);
            }
        }

        private static int FindTagEnd(string text, int start)
        {
            var quote = '\0';
            for (var index = start + 1; index < text.Length; index++)
            {
                var character = text[index];
                if (quote != '\0')
                {
                    if (character == quote)
                    {
                        quote = '\0';
                    }
                }
                else if (character == '\'' || character == '"')
                {
                    quote = character;
                }
                else if (character == '>')
                {
                    return index;
                }
            }

            throw new XmlException("Unterminated XML tag.");
        }

        private static bool IsXmlWhitespace(string text, int start, int end)
        {
            for (var index = start; index < end; index++)
            {
                var character = text[index];
                if (character != ' ' && character != '\t' && character != '\r' && character != '\n')
                {
                    return false;
                }
            }

            return true;
        }

        private static Node ParseLayout(string text, Queue<Node> elements)
        {
            // XML validity, names, entities and namespaces have already been checked by
            // XmlReader. This scan records original slices instead of reserializing them:
            // quote styles, entity spellings and attribute values must survive unchanged.
            var document = new Node();
            var stack = new Stack<Node>();
            stack.Push(document);
            var position = 0;
            while (position < text.Length)
            {
                var start = position;
                if (text[position] != '<')
                {
                    var next = text.IndexOf('<', position);
                    position = next < 0 ? text.Length : next;
                    var whitespace = IsXmlWhitespace(text, start, position);
                    stack.Peek().Children.Add(new Node { Start = start, End = position, IsWhitespace = whitespace });
                    if (!whitespace)
                    {
                        stack.Peek().Preserve = true;
                    }

                    continue;
                }

                if (StartsWith(text, position, "<!--") || StartsWith(text, position, "<![CDATA[") || StartsWith(text, position, "<?"))
                {
                    var cdata = StartsWith(text, position, "<![CDATA[");
                    var terminator = cdata ? "]]>" : StartsWith(text, position, "<!--") ? "-->" : "?>";
                    position = text.IndexOf(terminator, position, StringComparison.Ordinal) + terminator.Length;
                    stack.Peek().Children.Add(new Node { Start = start, End = position });
                    if (cdata)
                    {
                        stack.Peek().Preserve = true;
                    }

                    continue;
                }

                position = FindTagEnd(text, position) + 1;
                if (text[start + 1] == '/')
                {
                    var element = stack.Pop();
                    element.CloseStart = start;
                    element.End = position;
                }
                else
                {
                    var element = elements.Dequeue();
                    element.Start = start;
                    element.OpenEnd = position;
                    element.CloseStart = position;
                    element.End = position;
                    stack.Peek().Children.Add(element);
                    if (text[position - 2] != '/')
                    {
                        stack.Push(element);
                    }
                }
            }

            return document;
        }

        private static Queue<Node> ReadElements(string text, bool xaml, bool enforceDepthLimit = true)
        {
            var elements = new Queue<Node>();
            using (var input = new StringReader(text))
            using (var reader = XmlReader.Create(input, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreWhitespace = false,
            }))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (enforceDepthLimit && reader.Depth >= 256)
                        {
                            var location = (IXmlLineInfo)reader;
                            throw new XmlException("Formatting supports XML nesting up to 256 levels.", null,
                                location.LineNumber, location.LinePosition);
                        }

                        elements.Enqueue(new Node
                        {
                            IsElement = true,
                            Preserve = reader.XmlSpace == XmlSpace.Preserve
                                || (xaml && XamlWhitespacePolicy.IsTextContainer(reader.LocalName)),
                            IndentChildren = !xaml || XamlWhitespacePolicy.IsStructuralContainer(reader.NamespaceURI, reader.LocalName),
                        });
                    }
                }
            }

            return elements;
        }

        private static void Render(Node node, string text, StringBuilder output, int depth, string indent, string newLine)
        {
            if (!node.IsElement || node.Preserve || node.Children.Count == 0)
            {
                output.Append(text, node.Start, node.End - node.Start);
                return;
            }

            output.Append(text, node.Start, node.OpenEnd - node.Start);
            var block = node.IndentChildren && node.Children.Any(child => !child.IsWhitespace);
            foreach (var child in node.Children)
            {
                if (block && child.IsWhitespace)
                {
                    continue;
                }

                if (block)
                {
                    output.Append(newLine);
                    AppendIndent(output, depth + 1, indent);
                }

                Render(child, text, output, depth + 1, indent, newLine);
            }

            if (block)
            {
                output.Append(newLine);
                AppendIndent(output, depth, indent);
            }

            output.Append(text, node.CloseStart, node.End - node.CloseStart);
        }

        private static bool StartsWith(string text, int start, string value)
        {
            return string.CompareOrdinal(text, start, value, 0, value.Length) == 0;
        }

        private sealed class Node
        {
            public List<Node> Children { get; } = new List<Node>();
            public int CloseStart { get; set; }
            public int End { get; set; }
            public bool IndentChildren { get; set; }
            public bool IsElement { get; set; }
            public bool IsWhitespace { get; set; }
            public int OpenEnd { get; set; }
            public bool Preserve { get; set; }
            public int Start { get; set; }
        }
    }
}
