// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PNFmt
{
    // Operates on already validated tag slices, changing only whitespace before
    // complete attributes. XML parsing and protected-subtree selection belong to
    // XmlDocumentFormatter; this class never interprets attribute values.
    internal sealed class XmlAttributeWrapper
    {
        private readonly StringBuilder output = new StringBuilder();
        private readonly string style;
        private readonly string attributeIndent;
        private readonly string indent;
        private readonly string newLine;
        private readonly int tabWidth;
        private readonly int width;
        private long column;

        public XmlAttributeWrapper(IReadOnlyDictionary<string, string> settings, string indent, string newLine, int defaultTabWidth)
        {
            this.indent = indent;
            this.newLine = newLine;
            this.style = Option(settings, "xml_attribute_style");
            this.attributeIndent = Option(settings, "xml_attribute_indent");
            settings.TryGetValue("max_line_length", out var generalWidth);
            var widthSetting = Option(settings, "xml_max_line_length") ?? generalWidth;
            this.width = Option(settings, "xml_wrap_tags_and_pi") == "true" ? PositiveInteger(widthSetting, 0) : 0;
            settings.TryGetValue("tab_width", out var tabSetting);
            this.tabWidth = PositiveInteger(tabSetting, defaultTabWidth);
            // Bound visual alignment allocation for unreasonable tab-stop settings.
            if (this.tabWidth > 256)
            {
                this.tabWidth = defaultTabWidth;
            }
        }

        public bool Enabled => this.width > 0 || this.style == "on_single_line"
            || this.style == "first_attribute_on_single_line" || this.style == "on_different_lines";

        public void Append(string text, int start, int length)
        {
            this.output.Append(text, start, length);
            for (var index = start; index < start + length; index++)
            {
                this.column = this.Advance(this.column, text[index]);
            }
        }

        public void AppendTag(string text, int start, int end, int depth, bool declaration = false)
        {
            if ((!declaration && this.style == "do_not_touch") || (declaration && this.width == 0))
            {
                this.Append(text, start, end - start);
                return;
            }

            var position = start + (declaration ? 2 : 1);
            while (position < end && !IsWhitespace(text[position]) && text[position] != '/' && text[position] != '>')
            {
                position++;
            }

            this.Append(text, start, position - start);
            var continuation = string.Concat(Enumerable.Repeat(this.indent,
                depth + (!declaration && this.attributeIndent == "double_indent" ? 2 : 1)));
            long? firstColumn = null;
            while (position < end)
            {
                var gapStart = position;
                while (position < end && IsWhitespace(text[position]))
                {
                    position++;
                }
                if (position == end || text[position] == '/' || text[position] == '>' || text[position] == '?')
                {
                    this.Append(text, gapStart, end - gapStart);
                    break;
                }

                var attributeStart = position;
                // Names and '=' have already been validated. Quotes inside a value
                // must be entity-escaped, so the matching quote delimits the slice.
                while (text[position] != '\'' && text[position] != '"')
                {
                    position++;
                }
                var quote = text[position++];
                while (text[position] != quote)
                {
                    position++;
                }
                position++;

                var gap = text.Substring(gapStart, attributeStart - gapStart);
                if (!declaration)
                {
                    if (this.style == "on_different_lines" || (this.style == "first_attribute_on_single_line" && firstColumn.HasValue))
                    {
                        gap = this.newLine + continuation;
                    }
                    else if (this.style == "on_single_line" || this.style == "first_attribute_on_single_line")
                    {
                        gap = " ";
                    }
                }

                var suffix = position;
                while (suffix < end && IsWhitespace(text[suffix]))
                {
                    suffix++;
                }
                var last = suffix < end && (text[suffix] == '/' || text[suffix] == '>' || text[suffix] == '?');
                var measuredEnd = last ? end : position;
                if (this.width > 0 && gap.IndexOfAny(new[] { '\r', '\n' }) < 0
                    && !this.Fits(gap, text, attributeStart, measuredEnd))
                {
                    gap = this.newLine + continuation;
                }

                this.Append(gap, 0, gap.Length);
                if (!firstColumn.HasValue)
                {
                    firstColumn = this.column;
                    if (!declaration && this.attributeIndent == "align_by_first_attribute")
                    {
                        continuation = new string(' ', (int)this.column);
                    }
                }
                this.Append(text, attributeStart, position - attributeStart);
            }
        }

        public override string ToString() => this.output.ToString();

        private long Advance(long current, char character)
        {
            return character == '\r' || character == '\n' ? 0
                : character == '\t' ? current + this.tabWidth - (current % this.tabWidth) : current + 1;
        }

        private bool Fits(string gap, string text, int start, int end)
        {
            var projected = this.column;
            foreach (var character in gap)
            {
                projected = this.Advance(projected, character);
            }
            for (var index = start; index < end; index++)
            {
                // Only the line that can be shortened by a break before this
                // attribute matters. Newlines within values are never changed.
                if (text[index] == '\r' || text[index] == '\n')
                {
                    break;
                }
                projected = this.Advance(projected, text[index]);
                if (projected > this.width)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsWhitespace(char value) => value == ' ' || value == '\t' || value == '\r' || value == '\n';

        private static string Option(IReadOnlyDictionary<string, string> settings, string key)
        {
            foreach (var spelling in new[] { key, "resharper_" + key })
            {
                if (settings.TryGetValue(spelling, out var value)
                    && !string.Equals(value.Trim(), "unset", StringComparison.OrdinalIgnoreCase))
                {
                    return value.Trim().ToLowerInvariant();
                }
            }
            return null;
        }

        private static int PositiveInteger(string value, int fallback)
        {
            return int.TryParse(value, out var result) && result > 0 ? result : fallback;
        }
    }
}
