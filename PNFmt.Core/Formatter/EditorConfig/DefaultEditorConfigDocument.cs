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

        public static int CountLegacySettings(string text)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return LegacyEditorConfigSettingsMigration.Count(SplitLines(text));
        }

        public static string Update(
            string text,
            bool migrateLegacySettings = false,
            bool removeLegacySettings = false)
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

            IReadOnlyList<string> existingLines;
            if (startIndexes.Count == 0)
            {
                existingLines = lines;
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

                existingLines = lines.Take(startIndex)
                    .Concat(lines.Skip(endIndex + 1))
                    .ToArray();
            }

            existingLines = LegacyEditorConfigSettingsMigration.Apply(
                existingLines,
                migrateLegacySettings,
                removeLegacySettings);
            var firstSectionIndex = Enumerable.Range(0, existingLines.Count)
                .FirstOrDefault(index => IsSectionHeader(existingLines[index]));
            if (firstSectionIndex == 0 && !IsSectionHeader(existingLines[0]))
            {
                firstSectionIndex = existingLines.Count;
            }

            var output = new List<string>();
            AddWithoutTrailingBlankLines(output, existingLines.Take(firstSectionIndex));
            if (output.Count == 0)
            {
                output.Add("root = true");
            }

            AddBlankLine(output);
            output.AddRange(CreateDefaultBlock());

            var suffix = existingLines
                .Skip(firstSectionIndex)
                .SkipWhile(string.IsNullOrWhiteSpace)
                .ToArray();
            if (suffix.Length > 0)
            {
                AddBlankLine(output);
                AddWithoutTrailingBlankLines(output, suffix);
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

        private static bool IsSectionHeader(string line)
        {
            var trimmed = line.Trim();
            return trimmed.Length >= 2
                && trimmed[0] == '['
                && trimmed[trimmed.Length - 1] == ']';
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
