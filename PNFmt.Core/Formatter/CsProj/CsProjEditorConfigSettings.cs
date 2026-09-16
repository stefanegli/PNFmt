// Copyright (c) 2026 by Stefan Egli.All rights reserved

using System;
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

            int? parsedTabWidth = null;
            if (settings.TryGetValue("tab_width", out var tabWidth))
            {
                if (int.TryParse(tabWidth, out var width) && width > 0)
                {
                    isActive = true;
                    parsedTabWidth = width;
                }
            }

            var hasIndentSize = false;
            if (settings.TryGetValue("indent_size", out var indentSize))
            {
                if (int.TryParse(indentSize, out var parsedIndentSize)
                    && parsedIndentSize > 0)
                {
                    isActive = true;
                    this.IndentSize = parsedIndentSize;
                    hasIndentSize = true;
                }
                else if (string.Equals(indentSize, "tab", StringComparison.OrdinalIgnoreCase)
                    && parsedTabWidth.HasValue)
                {
                    isActive = true;
                    this.IndentSize = parsedTabWidth.Value;
                    hasIndentSize = true;
                }
            }

            if (!hasIndentSize && parsedTabWidth.HasValue)
            {
                this.IndentSize = parsedTabWidth.Value;
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

        private static char ResolveIndentStyle(string indentStyle)
        {
            return string.Equals(indentStyle, "tab", StringComparison.OrdinalIgnoreCase) ? '\t' : ' ';
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
    }
}
