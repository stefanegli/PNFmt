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

        private static readonly IReadOnlyList<DefaultSection> Sections =
            new[]
            {
                new DefaultSection(
                    "[*.cs]",
                    new DefaultSetting(EditorConfigSettingNames.CSharpFormat, "true"),
                    new DefaultSetting(EditorConfigSettingNames.SortEntries, "true")),
                new DefaultSection(
                    "[*.csproj]",
                    new DefaultSetting(EditorConfigSettingNames.CsProjEmptyLinesBetweenGroups, "1"),
                    new DefaultSetting(EditorConfigSettingNames.SortEntries, "true")),
                new DefaultSection(
                    "[*.editorconfig]",
                    new DefaultSetting(EditorConfigSettingNames.IniMergeGroups, "true"),
                    new DefaultSetting(EditorConfigSettingNames.SortEntries, "true")),
                new DefaultSection(
                    "[*.ini]",
                    new DefaultSetting(EditorConfigSettingNames.IniGroupByPrefix, "true"),
                    new DefaultSetting(EditorConfigSettingNames.IniMergeGroups, "true"),
                    new DefaultSetting(EditorConfigSettingNames.IniSortGroups, "true"),
                    new DefaultSetting(EditorConfigSettingNames.SortEntries, "true")),
                new DefaultSection(
                    "[*.resx]",
                    new DefaultSetting(EditorConfigSettingNames.ResxRemoveDocumentationComment, "true"),
                    new DefaultSetting(EditorConfigSettingNames.ResxRemoveXsdSchema, "true"),
                    new DefaultSetting(EditorConfigSettingNames.ResxSortComparer, "OrdinalIgnoreCase"),
                    new DefaultSetting(EditorConfigSettingNames.SortEntries, "true")),
                new DefaultSection(
                    "[*.rsp]",
                    new DefaultSetting(EditorConfigSettingNames.SortEntries, "true")),
                new DefaultSection(
                    "[*.slnx]",
                    new DefaultSetting(EditorConfigSettingNames.SortEntries, "true")),
            };

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
            var lines = RemoveLegacyManagedBlock(SplitLines(text));
            lines = LegacyEditorConfigSettingsMigration.Apply(
                    lines,
                    migrateLegacySettings,
                    removeLegacySettings)
                .ToList();

            RemoveTrailingBlankLines(lines);
            if (lines.Count == 0)
            {
                lines.Add("root = true");
            }

            foreach (var section in Sections)
            {
                MergeSection(lines, section);
            }

            RemoveTrailingBlankLines(lines);
            return string.Join(newLine, lines) + newLine;
        }

        private static void AppendSection(List<string> lines, DefaultSection section)
        {
            RemoveTrailingBlankLines(lines);
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add(section.Header);
            lines.AddRange(section.Settings.Select(setting => setting.Formatted));
        }

        private static IReadOnlyList<int> FindMarkerIndexes(
            IReadOnlyList<string> lines,
            string marker)
        {
            return Enumerable.Range(0, lines.Count)
                .Where(index => string.Equals(lines[index].Trim(), marker, StringComparison.Ordinal))
                .ToArray();
        }

        private static IReadOnlyList<int> FindSectionIndexes(
            IReadOnlyList<string> lines,
            string header)
        {
            return Enumerable.Range(0, lines.Count)
                .Where(index => string.Equals(
                    lines[index].Trim(),
                    header,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        private static int FindSectionEnd(IReadOnlyList<string> lines, int sectionIndex)
        {
            for (var index = sectionIndex + 1; index < lines.Count; index++)
            {
                if (IniSyntax.IsSectionHeader(lines[index]))
                {
                    return index;
                }
            }

            return lines.Count;
        }

        private static int FindSortedInsertionIndex(
            IReadOnlyList<string> lines,
            int sectionIndex,
            string key)
        {
            var sectionEnd = FindSectionEnd(lines, sectionIndex);
            var lastPropertyIndex = -1;
            for (var index = sectionIndex + 1; index < sectionEnd; index++)
            {
                if (!IniSyntax.TryParseProperty(
                    lines[index],
                    out var property,
                    allowColon: true))
                {
                    continue;
                }

                if (StringComparer.OrdinalIgnoreCase.Compare(property.Key, key) > 0)
                {
                    return index;
                }

                lastPropertyIndex = index;
            }

            return lastPropertyIndex >= 0 ? lastPropertyIndex + 1 : sectionIndex + 1;
        }

        private static HashSet<string> GetPropertyNames(
            IReadOnlyList<string> lines,
            IReadOnlyList<int> sectionIndexes)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sectionIndex in sectionIndexes)
            {
                var sectionEnd = FindSectionEnd(lines, sectionIndex);
                for (var index = sectionIndex + 1; index < sectionEnd; index++)
                {
                    if (IniSyntax.TryParseProperty(
                        lines[index],
                        out var property,
                        allowColon: true))
                    {
                        names.Add(property.Key);
                    }
                }
            }

            return names;
        }

        private static void MergeSection(List<string> lines, DefaultSection section)
        {
            var sectionIndexes = FindSectionIndexes(lines, section.Header);
            if (sectionIndexes.Count == 0)
            {
                AppendSection(lines, section);
                return;
            }

            var existingNames = GetPropertyNames(lines, sectionIndexes);
            var targetSectionIndex = sectionIndexes[0];
            foreach (var setting in section.Settings)
            {
                if (existingNames.Contains(setting.Key))
                {
                    continue;
                }

                var insertionIndex = FindSortedInsertionIndex(
                    lines,
                    targetSectionIndex,
                    setting.Key);
                lines.Insert(insertionIndex, setting.Formatted);
                existingNames.Add(setting.Key);
            }
        }

        private static List<string> RemoveLegacyManagedBlock(IReadOnlyList<string> lines)
        {
            var startIndexes = FindMarkerIndexes(lines, StartMarker);
            var endIndexes = FindMarkerIndexes(lines, EndMarker);
            if (startIndexes.Count != endIndexes.Count || startIndexes.Count > 1)
            {
                throw new InvalidDataException(
                    "The EditorConfig file contains incomplete or duplicate PNFmt default markers.");
            }

            if (startIndexes.Count == 0)
            {
                return lines.ToList();
            }

            var startIndex = startIndexes[0];
            var endIndex = endIndexes[0];
            if (endIndex < startIndex)
            {
                throw new InvalidDataException(
                    "The PNFmt default configuration end marker appears before its start marker.");
            }

            return lines.Take(startIndex)
                .Concat(lines.Skip(endIndex + 1))
                .ToList();
        }

        private static void RemoveTrailingBlankLines(List<string> lines)
        {
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
            {
                lines.RemoveAt(lines.Count - 1);
            }
        }

        private static IReadOnlyList<string> SplitLines(string text)
        {
            return text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split(new[] { '\n' }, StringSplitOptions.None);
        }

        private sealed class DefaultSection
        {
            public DefaultSection(string header, params DefaultSetting[] settings)
            {
                this.Header = header;
                this.Settings = settings;
            }

            public string Header { get; }

            public IReadOnlyList<DefaultSetting> Settings { get; }
        }

        private sealed class DefaultSetting
        {
            public DefaultSetting(string key, string value)
            {
                this.Key = key;
                this.Value = value;
            }

            public string Formatted => $"{this.Key} = {this.Value}";

            public string Key { get; }

            public string Value { get; }
        }
    }
}
