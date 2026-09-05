// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace PNFmt
{
    internal static class RspDocumentFormatter
    {
        public static string Format(string text)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
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

                FlushEntries(entries, output);
                if (hasBlankLineAfterEntries)
                {
                    AddBlankLine(output);
                    hasBlankLineAfterEntries = false;
                }

                output.Add(line.TrimEnd());
            }

            FlushEntries(entries, output);
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

        private static void FlushEntries(
            List<ResponseFileEntry> entries,
            List<string> output)
        {
            output.AddRange(entries
                .OrderBy(entry => entry.SortKey, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.SortKey, StringComparer.Ordinal)
                .Select(entry => entry.Text));
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
