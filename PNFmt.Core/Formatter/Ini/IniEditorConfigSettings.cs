// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;

namespace PNFmt
{
    internal sealed class IniEditorConfigSettings
    {
        public IniEditorConfigSettings(string targetFile, IFormatterLog log)
        {
            try
            {
                var parser = new EditorConfig.Core.EditorConfigParser();
                var properties = parser.Parse(targetFile).Properties;
                this.SortEntries = IsEnabled(properties, EditorConfigSettingNames.SortEntries);
                this.SortGroups = IsEnabled(properties, EditorConfigSettingNames.IniSortGroups);
            }
            catch (Exception ex)
            {
                log?.WriteLine("Failed to parse EditorConfig file:\n" + ex.ToString());
            }

            this.IsActive = this.SortEntries || this.SortGroups;
        }

        public bool IsActive { get; }

        public bool SortEntries { get; }

        public bool SortGroups { get; }

        private static bool IsEnabled(
            System.Collections.Generic.IReadOnlyDictionary<string, string> properties,
            string settingName)
        {
            return properties.TryGetValue(settingName, out var value)
                && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
