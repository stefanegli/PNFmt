// Copyright (c) 2026 by Stefan Egli.All rights reserved

using System;
using System.Collections.Generic;
using System.Linq;

namespace PNFmt
{
    internal sealed class CsProjEditorConfigSettings : ICsProjFormatSettings
    {
        public CsProjEditorConfigSettings(string targetFile = "dummy.csproj", IFormatterLog log = null)
            : this(FileFormattingConfiguration.Load(targetFile, log))
        {
        }

        internal CsProjEditorConfigSettings(FileFormattingConfiguration configuration)
        {
            var isActive = false;
            var settings = configuration.Properties;
            var resolver = configuration.CreateSettingResolver();
            if (resolver.TryGet(
                LegacyEditorConfigSettingAliases.CsProjSortEntries,
                out var sortEntries))
            {
                isActive = true;
                this.SortEntries = EditorConfigSettings.IsEnabled(sortEntries);
            }

            if (settings.TryGetValue("indent_style", out var indentStyle))
            {
                isActive = true;
                this.IndentStyle = ResolveIndentStyle(indentStyle);
            }

            // A numeric indent size takes precedence; "tab", invalid, and missing
            // sizes all fall back to a valid tab width, then to the default.
            var indentSize = ReadPositiveInteger(settings, "indent_size")
                ?? ReadPositiveInteger(settings, "tab_width");
            if (indentSize.HasValue)
            {
                isActive = true;
                this.IndentSize = indentSize.Value;
            }

            if (settings.TryGetValue("end_of_line", out var endOfLine))
            {
                isActive = true;
                this.EndOfLine = ResolveEndOfLine(endOfLine);
            }

            if (settings.TryGetValue("insert_final_newline", out var insertFinalNewline)
                && bool.TryParse(insertFinalNewline, out var parsedInsertFinalNewline))
            {
                isActive = true;
                this.InsertFinalNewline = parsedInsertFinalNewline;
            }

            if (resolver.TryGet(
                    LegacyEditorConfigSettingAliases.CsProjEmptyLinesBetweenGroups,
                    out var emptyLinesBetweenGroups)
                && int.TryParse(emptyLinesBetweenGroups, out var parsedEmptyLinesBetweenGroups)
                && parsedEmptyLinesBetweenGroups >= 0)
            {
                isActive = true;
                this.EmptyLinesBetweenGroups = parsedEmptyLinesBetweenGroups;
            }

            if (resolver.TryGet(
                LegacyEditorConfigSettingAliases.CsProjSortItemTypes,
                out var sortItemTypes))
            {
                isActive = true;
                var parsedItemTypes = sortItemTypes
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => x.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (parsedItemTypes.Length > 0)
                {
                    this.SortItemTypes = parsedItemTypes;
                }
            }

            this.IsActive = isActive;
        }

        public bool IsActive { get; }

        public bool SortEntries { get; }

        public System.Collections.Generic.IReadOnlyCollection<string> SortItemTypes { get; private set; } = CsProjItemSorting.Defaults;

        public int IndentSize { get; } = 2;

        public char IndentStyle { get; } = ' ';

        public string EndOfLine { get; } = "\r\n";

        public bool InsertFinalNewline { get; } = true;

        public int EmptyLinesBetweenGroups { get; } = 1;

        private static int? ReadPositiveInteger(IReadOnlyDictionary<string, string> settings, string name)
        {
            return settings.TryGetValue(name, out var value)
                && int.TryParse(value, out var parsed) && parsed > 0 ? parsed : (int?)null;
        }

        private static string ResolveEndOfLine(string endOfLine)
        {
            if (string.Equals(endOfLine, "lf", StringComparison.OrdinalIgnoreCase))
            {
                return "\n";
            }

            if (string.Equals(endOfLine, "cr", StringComparison.OrdinalIgnoreCase))
            {
                return "\r";
            }

            return "\r\n";
        }

        private static char ResolveIndentStyle(string indentStyle)
        {
            return string.Equals(indentStyle, "tab", StringComparison.OrdinalIgnoreCase) ? '\t' : ' ';
        }
    }
}
