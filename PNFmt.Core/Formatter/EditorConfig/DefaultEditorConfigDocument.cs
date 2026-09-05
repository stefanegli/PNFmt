// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PNFmt
{
    internal static class DefaultEditorConfigDocument
    {
        public const string EndMarker = "# </pnfmt-defaults>";
        public const string StartMarker = "# <pnfmt-defaults>";

        public static string Update(string text)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var newLine = text.Length == 0
                ? Environment.NewLine
                : TextFileFormatting.DetectNewLine(text);
            var lines = SplitLines(text);
            var startIndexes = FindMarkerIndexes(lines, StartMarker);
            var endIndexes = FindMarkerIndexes(lines, EndMarker);
            if (startIndexes.Count != endIndexes.Count || startIndexes.Count > 1)
            {
                throw new InvalidDataException(
                    "The EditorConfig file contains incomplete or duplicate PNFmt default markers.");
            }

            var output = new List<string>();
            if (startIndexes.Count == 0)
            {
                AddWithoutTrailingBlankLines(output, lines);
                if (output.Count == 0)
                {
                    output.Add("root = true");
                }

                AddBlankLine(output);
                output.AddRange(CreateDefaultBlock());
            }
            else
            {
                var startIndex = startIndexes[0];
                var endIndex = endIndexes[0];
                if (endIndex < startIndex)
                {
                    throw new InvalidDataException(
                        "The PNFmt default configuration end marker appears before its start marker.");
                }

                AddWithoutTrailingBlankLines(output, lines.Take(startIndex));
                AddBlankLine(output);
                output.AddRange(CreateDefaultBlock());

                var suffix = lines.Skip(endIndex + 1).SkipWhile(string.IsNullOrWhiteSpace).ToArray();
                if (suffix.Length > 0)
                {
                    AddBlankLine(output);
                    AddWithoutTrailingBlankLines(output, suffix);
                }
            }

            return string.Join(newLine, output) + newLine;
        }

        private static void AddBlankLine(List<string> output)
        {
            if (output.Count > 0 && output[output.Count - 1].Length > 0)
            {
                output.Add(string.Empty);
            }
        }

        private static void AddWithoutTrailingBlankLines(
            List<string> output,
            IEnumerable<string> lines)
        {
            var materializedLines = lines.ToList();
            while (materializedLines.Count > 0
                && string.IsNullOrWhiteSpace(materializedLines[materializedLines.Count - 1]))
            {
                materializedLines.RemoveAt(materializedLines.Count - 1);
            }

            output.AddRange(materializedLines);
        }

        private static IReadOnlyCollection<string> CreateDefaultBlock()
        {
            return new[]
            {
                StartMarker,
                string.Empty,
                "[*.csproj]",
                $"{EditorConfigSettingNames.CsProjEmptyLinesBetweenGroups} = 1",
                $"{EditorConfigSettingNames.SortEntries} = true",
                string.Empty,
                "[*.editorconfig]",
                $"{EditorConfigSettingNames.SortEntries} = true",
                string.Empty,
                "[*.ini]",
                $"{EditorConfigSettingNames.IniGroupByPrefix} = true",
                $"{EditorConfigSettingNames.IniSortGroups} = true",
                $"{EditorConfigSettingNames.SortEntries} = true",
                string.Empty,
                "[*.resx]",
                $"{EditorConfigSettingNames.ResxRemoveDocumentationComment} = true",
                $"{EditorConfigSettingNames.ResxRemoveXsdSchema} = true",
                $"{EditorConfigSettingNames.ResxSortComparer} = OrdinalIgnoreCase",
                $"{EditorConfigSettingNames.SortEntries} = true",
                string.Empty,
                "[*.rsp]",
                $"{EditorConfigSettingNames.SortEntries} = true",
                string.Empty,
                "[*.slnx]",
                $"{EditorConfigSettingNames.SortEntries} = true",
                string.Empty,
                EndMarker,
            };
        }

        private static IReadOnlyList<int> FindMarkerIndexes(
            IReadOnlyList<string> lines,
            string marker)
        {
            return Enumerable.Range(0, lines.Count)
                .Where(index => string.Equals(lines[index].Trim(), marker, StringComparison.Ordinal))
                .ToArray();
        }

        private static IReadOnlyList<string> SplitLines(string text)
        {
            return text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split(new[] { '\n' }, StringSplitOptions.None);
        }
    }
}
