// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace PNFmt
{
    internal static class IniDocumentFormatter
    {
        public static string Format(
            string text,
            bool sortEntries = true,
            bool sortGroups = false,
            bool groupByPrefix = false,
            bool mergeGroups = false,
            bool isEditorConfig = false)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var newLine = TextFileFormatting.DetectNewLine(text);
            IReadOnlyList<string> lines = text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split(new[] { '\n' }, StringSplitOptions.None);
            if (mergeGroups)
            {
                lines = isEditorConfig ? MergeAdjacentGroups(lines) : MergeGroups(lines);
            }

            var output = new List<string>();
            var properties = new List<PropertyLine>();
            var hasBlankLineAfterProperties = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (TryParseProperty(trimmed, isEditorConfig, out var property))
                {
                    hasBlankLineAfterProperties = false;
                    properties.Add(property);
                    continue;
                }

                if (trimmed.Length == 0)
                {
                    if (properties.Count > 0)
                    {
                        hasBlankLineAfterProperties = true;
                    }
                    else
                    {
                        AddBlankLine(output);
                    }

                    continue;
                }

                FlushProperties(properties, output, sortEntries, groupByPrefix);
                if (hasBlankLineAfterProperties)
                {
                    AddBlankLine(output);
                    hasBlankLineAfterProperties = false;
                }

                output.Add(IsSectionHeader(trimmed) ? trimmed : line.TrimEnd());
            }

            FlushProperties(properties, output, sortEntries, groupByPrefix);
            while (output.Count > 0 && output[output.Count - 1].Length == 0)
            {
                output.RemoveAt(output.Count - 1);
            }

            // Glob overlap cannot be established from lexical header order. Keep
            // EditorConfig sections in occurrence order even when sorting is requested.
            var orderedOutput = sortGroups && !isEditorConfig ? SortGroups(output) : output;
            return orderedOutput.Count == 0
                ? string.Empty
                : string.Join(newLine, orderedOutput) + newLine;
        }

        private static void AddBlankLine(List<string> output)
        {
            if (output.Count > 0 && output[output.Count - 1].Length > 0)
            {
                output.Add(string.Empty);
            }
        }

        private static void FlushProperties(
            List<PropertyLine> properties,
            List<string> output,
            bool sortEntries,
            bool groupByPrefix)
        {
            // OrderBy is stable: duplicate keys (including casing variants) must
            // retain their assignment order, also after merging adjacent sections.
            IEnumerable<PropertyLine> orderedProperties = sortEntries || groupByPrefix
                ? properties.OrderBy(property => property.Key, StringComparer.OrdinalIgnoreCase)
                : properties;

            if (groupByPrefix)
            {
                AddPrefixGroups(orderedProperties.ToArray(), output);
            }
            else
            {
                output.AddRange(orderedProperties.Select(property => property.Formatted));
            }

            properties.Clear();
        }

        private static void AddPrefixGroups(
            IReadOnlyList<PropertyLine> properties,
            List<string> output)
        {
            var prefixCounts = properties
                .Where(property => property.Prefix is not null)
                .GroupBy(property => property.Prefix, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
            string previousGroup = null;
            for (var index = 0; index < properties.Count; index++)
            {
                var property = properties[index];
                var group = property.Prefix is not null && prefixCounts[property.Prefix] > 1
                    ? property.Prefix
                    : null;
                if (index > 0
                    && !string.Equals(previousGroup, group, StringComparison.OrdinalIgnoreCase)
                    && (previousGroup is not null || group is not null))
                {
                    AddBlankLine(output);
                }

                output.Add(property.Formatted);
                previousGroup = group;
            }
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
            return IniSyntax.IsSectionHeader(line);
        }

        private static bool TryParseProperty(string line, bool isEditorConfig, out PropertyLine property)
        {
            if (!IniSyntax.TryParseProperty(line, out var parsedProperty, allowColon: isEditorConfig))
            {
                property = null;
                return false;
            }

            var prefixSeparator = parsedProperty.Key.IndexOf('_');
            var prefix = prefixSeparator > 0
                ? parsedProperty.Key.Substring(0, prefixSeparator)
                : null;
            property = new PropertyLine(
                parsedProperty.Key,
                prefix,
                parsedProperty.Formatted);
            return true;
        }

        private static IReadOnlyList<string> MergeAdjacentGroups(IReadOnlyList<string> lines)
        {
            var output = new List<string>();
            string previousHeader = null;
            foreach (var line in lines)
            {
                if (IsSectionHeader(line))
                {
                    var header = line.Trim();
                    if (string.Equals(previousHeader, header, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    previousHeader = header;
                }

                output.Add(line);
            }

            return output;
        }

        private static IReadOnlyList<string> MergeGroups(IReadOnlyList<string> lines)
        {
            var sectionStarts = Enumerable.Range(0, lines.Count)
                .Where(index => IsSectionHeader(lines[index].Trim()))
                .ToArray();
            if (sectionStarts.Length < 2)
            {
                return lines;
            }

            var groups = new List<SectionGroup>();
            var groupsByName = new Dictionary<string, SectionGroup>(
                StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < sectionStarts.Length; index++)
            {
                var start = sectionStarts[index];
                var end = index + 1 < sectionStarts.Length
                    ? sectionStarts[index + 1]
                    : lines.Count;
                var name = lines[start].Trim();
                var body = lines.Skip(start + 1).Take(end - start - 1).ToList();
                TrimBoundaryBlankLines(body);

                if (groupsByName.TryGetValue(name, out var existingGroup))
                {
                    existingGroup.Append(body);
                }
                else
                {
                    var group = new SectionGroup(name, new[] { name }.Concat(body));
                    groups.Add(group);
                    groupsByName.Add(name, group);
                }
            }

            var output = lines.Take(sectionStarts[0]).ToList();
            while (output.Count > 0 && string.IsNullOrWhiteSpace(output[output.Count - 1]))
            {
                output.RemoveAt(output.Count - 1);
            }

            foreach (var group in groups)
            {
                AddBlankLine(output);
                output.AddRange(group.Lines);
            }

            return output;
        }

        private static void TrimBoundaryBlankLines(List<string> lines)
        {
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
            {
                lines.RemoveAt(0);
            }

            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
            {
                lines.RemoveAt(lines.Count - 1);
            }
        }

        private sealed class PropertyLine
        {
            public PropertyLine(string key, string prefix, string formatted)
            {
                this.Key = key;
                this.Prefix = prefix;
                this.Formatted = formatted;
            }

            public string Formatted { get; }

            public string Key { get; }

            public string Prefix { get; }
        }

        private sealed class SectionGroup
        {
            private readonly List<string> lines;

            public SectionGroup(string name, IEnumerable<string> lines)
            {
                this.Name = name;
                this.lines = lines.ToList();
            }

            public IReadOnlyCollection<string> Lines => this.lines;

            public string Name { get; }

            public void Append(IEnumerable<string> appendedLines)
            {
                this.lines.AddRange(appendedLines);
            }
        }
    }
}
