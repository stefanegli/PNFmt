// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PNFmt
{
    internal static class LegacyEditorConfigSettingsMigration
    {
        private static readonly IReadOnlyDictionary<string, EditorConfigSettingAlias> Aliases =
            LegacyEditorConfigSettingAliases.All.ToDictionary(
                alias => alias.LegacyName,
                StringComparer.OrdinalIgnoreCase);

        public static int Count(IReadOnlyList<string> lines)
        {
            return lines.Count(line => TryGetAlias(line, out _, out _));
        }

        public static IReadOnlyList<string> Apply(
            IReadOnlyList<string> lines,
            bool migrate,
            bool removeLegacy)
        {
            if (!migrate && !removeLegacy)
            {
                return lines;
            }

            var output = new List<string>();
            var block = new List<string>();
            foreach (var line in lines)
            {
                if (IsSectionHeader(line) && block.Count > 0)
                {
                    AddBlock(block, output, migrate, removeLegacy);
                    block.Clear();
                }

                block.Add(line);
            }

            AddBlock(block, output, migrate, removeLegacy);
            return output;
        }

        private static void AddBlock(
            IReadOnlyList<string> block,
            List<string> output,
            bool migrate,
            bool removeLegacy)
        {
            var currentNames = new HashSet<string>(
                block.Select(GetPropertyName).Where(name => name is not null),
                StringComparer.OrdinalIgnoreCase);
            var migratedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in block)
            {
                if (!TryGetAlias(line, out var alias, out var separatorIndex))
                {
                    output.Add(line);
                    continue;
                }

                var shouldAddCurrent = migrate && !currentNames.Contains(alias.CurrentName);
                if (shouldAddCurrent)
                {
                    var value = line.Substring(separatorIndex + 1).Trim();
                    if (migratedValues.TryGetValue(alias.CurrentName, out var migratedValue))
                    {
                        if (!string.Equals(value, migratedValue, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException(
                                $"Legacy settings in the same EditorConfig section map to "
                                + $"'{alias.CurrentName}' with conflicting values.");
                        }

                        shouldAddCurrent = false;
                    }
                    else
                    {
                        migratedValues.Add(alias.CurrentName, value);
                    }
                }

                if (!removeLegacy)
                {
                    output.Add(line);
                }

                if (shouldAddCurrent)
                {
                    output.Add(
                        alias.CurrentName
                        + " = "
                        + line.Substring(separatorIndex + 1).Trim());
                }
            }
        }

        private static string GetPropertyName(string line)
        {
            return IniSyntax.TryParseProperty(line, out var property, allowColon: true)
                ? property.Key
                : null;
        }

        private static bool IsSectionHeader(string line)
        {
            return IniSyntax.IsSectionHeader(line);
        }

        private static bool TryGetAlias(
            string line,
            out EditorConfigSettingAlias alias,
            out int separatorIndex)
        {
            alias = null;
            separatorIndex = -1;
            if (!IniSyntax.TryParseProperty(line, out var property, allowColon: true)
                || !Aliases.TryGetValue(property.Key, out alias))
            {
                return false;
            }

            separatorIndex = property.SeparatorIndex;
            return true;
        }
    }
}
