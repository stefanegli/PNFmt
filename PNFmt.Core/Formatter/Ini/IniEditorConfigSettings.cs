// Copyright (c) 2026 by Stefan Egli. All rights reserved.

namespace PNFmt
{
    internal sealed class IniEditorConfigSettings
    {
        public IniEditorConfigSettings(string targetFile, IFormatterLog log)
            : this(FileFormattingConfiguration.Load(targetFile, log))
        {
        }

        internal IniEditorConfigSettings(FileFormattingConfiguration configuration)
        {
            var settings = configuration.Properties;
            this.FormatLayout = configuration.FormatLayout("ini");
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

            // Legacy prefix grouping also sorted entries. Explicit configurations
            // control sorting independently from grouping.
            if (this.GroupByPrefix && configuration.IsLegacy)
            {
                this.SortEntries = true;
            }

            this.IsActive = this.SortEntries
                || this.GroupByPrefix
                || this.MergeGroups
                || this.SortGroups;
        }

        public bool GroupByPrefix { get; }
        public bool FormatLayout { get; }

        public bool IsActive { get; }

        public bool MergeGroups { get; }

        public bool SortEntries { get; }

        public bool SortGroups { get; }
    }
}
