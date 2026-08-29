// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace PNFmt
{
    internal static class IniDocumentFormatter
    {
        public static string Format(string text)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var newLine = DetectNewLine(text);
            var lines = text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split(new[] { '\n' }, StringSplitOptions.None);
            var output = new List<string>();
            var properties = new List<PropertyLine>();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (TryParseProperty(trimmed, out var property))
                {
                    properties.Add(property);
                    continue;
                }

                FlushProperties(properties, output);
                if (trimmed.Length == 0)
                {
                    if (output.Count > 0 && output[output.Count - 1].Length > 0)
                    {
                        output.Add(string.Empty);
                    }

                    continue;
                }

                output.Add(IsSectionHeader(trimmed) ? trimmed : line.TrimEnd());
            }

            FlushProperties(properties, output);
            while (output.Count > 0 && output[output.Count - 1].Length == 0)
            {
                output.RemoveAt(output.Count - 1);
            }

            return output.Count == 0
                ? string.Empty
                : string.Join(newLine, output) + newLine;
        }

        private static string DetectNewLine(string text)
        {
            if (text.IndexOf("\r\n", StringComparison.Ordinal) >= 0)
            {
                return "\r\n";
            }

            if (text.IndexOf('\r') >= 0)
            {
                return "\r";
            }

            return "\n";
        }

        private static void FlushProperties(List<PropertyLine> properties, List<string> output)
        {
            output.AddRange(
                properties
                    .OrderBy(property => property.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(property => property.Formatted));
            properties.Clear();
        }

        private static bool IsSectionHeader(string line)
        {
            return line.Length >= 2
                && line[0] == '['
                && line[line.Length - 1] == ']';
        }

        private static bool TryParseProperty(string line, out PropertyLine property)
        {
            property = null;
            if (line.Length == 0
                || line[0] == '#'
                || line[0] == ';'
                || IsSectionHeader(line))
            {
                return false;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                return false;
            }

            var key = line.Substring(0, separator).Trim();
            if (key.Length == 0)
            {
                return false;
            }

            var value = line.Substring(separator + 1).Trim();
            property = new PropertyLine(key, $"{key} = {value}");
            return true;
        }

        private sealed class PropertyLine
        {
            public PropertyLine(string key, string formatted)
            {
                this.Key = key;
                this.Formatted = formatted;
            }

            public string Formatted { get; }

            public string Key { get; }
        }
    }
}
