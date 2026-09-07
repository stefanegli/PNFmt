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
            this.GroupByPrefix = EditorConfigSettings.IsEnabled(
                settings,
                EditorConfigSettingNames.IniGroupByPrefix);
            this.MergeGroups = EditorConfigSettings.IsEnabled(
                settings,
                EditorConfigSettingNames.IniMergeGroups);
            this.SortGroups = EditorConfigSettings.IsEnabled(
                settings,
                EditorConfigSettingNames.IniSortGroups);

            this.IsActive = this.SortEntries
                || this.GroupByPrefix
                || this.MergeGroups
                || this.SortGroups;
        }

        public bool GroupByPrefix { get; }

        public bool IsActive { get; }

        public bool MergeGroups { get; }

        public bool SortEntries { get; }

        public bool SortGroups { get; }
    }
}
