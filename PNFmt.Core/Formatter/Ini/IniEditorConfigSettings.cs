// Copyright (c) 2026 by Stefan Egli. All rights reserved.

namespace PNFmt
{
    internal sealed class IniEditorConfigSettings
    {
        public IniEditorConfigSettings(string targetFile, IFormatterLog log)
        {
            var settings = EditorConfigSettings.Load(targetFile, log);
            this.SortEntries = EditorConfigSettings.IsEnabled(
                settings,
                EditorConfigSettingNames.SortEntries);
            this.SortGroups = EditorConfigSettings.IsEnabled(
                settings,
                EditorConfigSettingNames.IniSortGroups);

            this.IsActive = this.SortEntries || this.SortGroups;
        }

        public bool IsActive { get; }

        public bool SortEntries { get; }

        public bool SortGroups { get; }
    }
}
