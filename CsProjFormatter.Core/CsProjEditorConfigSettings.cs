// Copyright (c) 2026 by Stefan Egli.All rights reserved

using System;
using System.Linq;

namespace CsProjFormatter
{
    internal class CsProjEditorConfigSettings : ISettings, IItemSortingSettings
    {
        public CsProjEditorConfigSettings(string targetFile = "dummy.csproj", ILog log = null)
        {
            var isActive = false;
            try
            {
                var parser = new EditorConfig.Core.EditorConfigParser();
                var settings = parser.Parse(targetFile).Properties;
                if (settings.TryGetValue("csproj_formatter_sort_entries", out var sortEntries))
                {
                    isActive = true;
                    this.SortEntries = IsEnabled(sortEntries);
                }

                if (settings.TryGetValue("indent_style", out var indentStyle))
                {
                    isActive = true;
                    this.IndentStyle = ResolveIndentStyle(indentStyle);
                }

                if (settings.TryGetValue("tab_width", out var tabWidth)
                    && int.TryParse(tabWidth, out var parsedTabWidth)
                    && parsedTabWidth > 0)
                {
                    isActive = true;
                    this.TabWidth = parsedTabWidth;
                }

                if (settings.TryGetValue("indent_size", out var indentSize)
                    && int.TryParse(indentSize, out var parsedIndentSize)
                    && parsedIndentSize > 0)
                {
                    isActive = true;
                    if (this.TabWidth == 0)
                    {
                        this.TabWidth = parsedIndentSize;
                    }
                }

                if (settings.TryGetValue("end_of_line", out var endOfLine))
                {
                    isActive = true;
                    this.EndOfLine = ResolveEndOfLine(endOfLine);
                }

                if (settings.TryGetValue("csproj_formatter_empty_lines_between_groups", out var emptyLinesBetweenGroups)
                    && int.TryParse(emptyLinesBetweenGroups, out var parsedEmptyLinesBetweenGroups)
                    && parsedEmptyLinesBetweenGroups >= 0)
                {
                    isActive = true;
                    this.EmptyLinesBetweenGroups = parsedEmptyLinesBetweenGroups;
                }

                if (settings.TryGetValue("csproj_formatter_sort_item_types", out var sortItemTypes))
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
            }
            catch (Exception ex)
            {
                log?.WriteLine("Failed to parse EditorConfig file:\n" + ex.ToString());
            }

            this.IsActive = isActive;

            bool IsEnabled(string setting) => string.Equals(setting, "true", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsActive { get; }

        public bool SortEntries { get; }

        public System.Collections.Generic.IReadOnlyCollection<string> SortItemTypes { get; private set; } = ItemSortingSettings.Defaults;

        public char IndentStyle { get; } = ' ';

        public int TabWidth { get; } = 2;

        public string EndOfLine { get; } = "\r\n";

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
