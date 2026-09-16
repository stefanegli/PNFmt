// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PNFmt
{
    internal static class RspDocumentFormatter
    {
        public static string Format(string text, bool sortEntries = true, bool formatLayout = true)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (!formatLayout)
            {
                return sortEntries ? SortWithoutFormatting(text) : text;
            }

            var newLine = TextFileFormatting.DetectNewLine(text);
            var lines = text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split(new[] { '\n' }, StringSplitOptions.None);
            var output = new List<string>();
            var entries = new List<ResponseFileEntry>();
            var hasBlankLineAfterEntries = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                {
                    if (entries.Count > 0)
                    {
                        hasBlankLineAfterEntries = true;
                    }
                    else
                    {
                        AddBlankLine(output);
                    }

                    continue;
                }

                if (trimmed[0] != '#')
                {
                    hasBlankLineAfterEntries = false;
                    entries.Add(new ResponseFileEntry(trimmed, line.TrimEnd()));
                    continue;
                }

                FlushEntries(entries, output, sortEntries);
                if (hasBlankLineAfterEntries)
                {
                    AddBlankLine(output);
                    hasBlankLineAfterEntries = false;
                }

                output.Add(line.TrimEnd());
            }

            FlushEntries(entries, output, sortEntries);
            while (output.Count > 0 && output[output.Count - 1].Length == 0)
            {
                output.RemoveAt(output.Count - 1);
            }

            return output.Count == 0
                ? string.Empty
                : string.Join(newLine, output) + newLine;
        }

        private static void AddBlankLine(List<string> output)
        {
            if (output.Count > 0 && output[output.Count - 1].Length > 0)
            {
                output.Add(string.Empty);
            }
        }

        private static string SortWithoutFormatting(string text)
        {
            // Keep separators in their original slots, including a missing final newline.
            var parts = Regex.Split(text, "(\r\n|\r|\n)");
            var indexes = new List<int>();
            for (var index = 0; index < parts.Length; index += 2)
            {
                var line = parts[index].Trim();
                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    Flush();
                }
                else if (line.Length > 0)
                {
                    indexes.Add(index);
                }
            }

            Flush();
            return string.Concat(parts);

            void Flush()
            {
                var sorted = indexes.Select(index => parts[index])
                    .OrderBy(line => line.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(line => line.Trim(), StringComparer.Ordinal).ToArray();
                for (var index = 0; index < indexes.Count; index++)
                {
                    parts[indexes[index]] = sorted[index];
                }

                indexes.Clear();
            }
        }

        private static void FlushEntries(
            List<ResponseFileEntry> entries,
            List<string> output,
            bool sortEntries)
        {
            var ordered = sortEntries
                ? entries.OrderBy(entry => entry.SortKey, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.SortKey, StringComparer.Ordinal)
                : (IEnumerable<ResponseFileEntry>)entries;
            output.AddRange(ordered.Select(entry => entry.Text));
            entries.Clear();
        }

        private sealed class ResponseFileEntry
        {
            public ResponseFileEntry(string sortKey, string text)
            {
                this.SortKey = sortKey;
                this.Text = text;
            }

            public string SortKey { get; }

            public string Text { get; }
        }
    }
}
