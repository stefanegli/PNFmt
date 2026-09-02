// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace PNFmt
{
    internal static class IniDocumentFormatter
    {
        public static string Format(string text, bool sortEntries = true, bool sortGroups = false)
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

                FlushProperties(properties, output, sortEntries);
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

            FlushProperties(properties, output, sortEntries);
            while (output.Count > 0 && output[output.Count - 1].Length == 0)
            {
                output.RemoveAt(output.Count - 1);
            }

            var orderedOutput = sortGroups ? SortGroups(output) : output;
            return orderedOutput.Count == 0
                ? string.Empty
                : string.Join(newLine, orderedOutput) + newLine;
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

        private static void FlushProperties(
            List<PropertyLine> properties,
            List<string> output,
            bool sortEntries)
        {
            IEnumerable<PropertyLine> orderedProperties = sortEntries
                ? properties.OrderBy(property => property.Key, StringComparer.OrdinalIgnoreCase)
                : properties;
            output.AddRange(orderedProperties.Select(property => property.Formatted));
            properties.Clear();
        }

        private static List<string> SortGroups(IReadOnlyList<string> lines)
        {
            var sectionStarts = Enumerable.Range(0, lines.Count)
                .Where(index => IsSectionHeader(lines[index]))
                .ToArray();
            if (sectionStarts.Length < 2)
            {
                return lines.ToList();
            }

            var output = lines.Take(sectionStarts[0]).ToList();
            var groups = new List<SectionGroup>();
            var separators = new List<IReadOnlyCollection<string>>();
            for (var index = 0; index < sectionStarts.Length; index++)
            {
                var start = sectionStarts[index];
                var end = index + 1 < sectionStarts.Length
                    ? sectionStarts[index + 1]
                    : lines.Count;
                var contentEnd = end;
                if (index + 1 < sectionStarts.Length)
                {
                    while (contentEnd > start + 1 && lines[contentEnd - 1].Length == 0)
                    {
                        contentEnd--;
                    }

                    separators.Add(lines.Skip(contentEnd).Take(end - contentEnd).ToArray());
                }

                groups.Add(new SectionGroup(
                    lines[start],
                    lines.Skip(start).Take(contentEnd - start).ToArray()));
            }

            var orderedGroups = groups.OrderBy(
                group => group.Name,
                StringComparer.OrdinalIgnoreCase).ToArray();
            for (var index = 0; index < orderedGroups.Length; index++)
            {
                output.AddRange(orderedGroups[index].Lines);
                if (index < separators.Count)
                {
                    output.AddRange(separators[index]);
                }
            }

            return output;
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

        private sealed class SectionGroup
        {
            public SectionGroup(string name, IReadOnlyCollection<string> lines)
            {
                this.Name = name;
                this.Lines = lines;
            }

            public IReadOnlyCollection<string> Lines { get; }

            public string Name { get; }
        }
    }
}
