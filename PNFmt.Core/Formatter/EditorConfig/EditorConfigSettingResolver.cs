// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;

namespace PNFmt
{
    internal sealed class EditorConfigSettingResolver
    {
        private readonly IFormatterLog log;
        private readonly IReadOnlyDictionary<string, string> properties;
        private readonly string targetFile;

        public EditorConfigSettingResolver(
            IReadOnlyDictionary<string, string> properties,
            string targetFile,
            IFormatterLog log)
        {
            this.properties = properties ?? throw new ArgumentNullException(nameof(properties));
            this.targetFile = targetFile ?? throw new ArgumentNullException(nameof(targetFile));
            this.log = log;
        }

        public bool TryGet(EditorConfigSettingAlias setting, out string value)
        {
            var hasSetting = this.properties.TryGetValue(setting.CurrentName, out var settingValue);
            var hasLegacySetting = this.properties.TryGetValue(
                setting.LegacyName,
                out var legacySettingValue);

            if (hasLegacySetting)
            {
                var message = hasSetting
                    ? $"EditorConfig setting '{setting.LegacyName}' is deprecated and ignored because "
                        + $"'{setting.CurrentName}' is set."
                    : $"EditorConfig setting '{setting.LegacyName}' is deprecated; use "
                        + $"'{setting.CurrentName}' instead.";
                this.log?.WriteLine($"{this.targetFile}: warning PNFMT001: {message}");
            }

            if (hasSetting)
            {
                value = settingValue;
                return true;
            }

            value = legacySettingValue;
            return hasLegacySetting;
        }
    }

    internal static class EditorConfigSettingNames
    {
        public const string CSharpFormat = "pnfmt_csharp_format";
        public const string CSharpCollapseBlankLines = "pnfmt_csharp_collapse_blank_lines";
        public const string CSharpMemberAccessibilityOrder = "pnfmt_csharp_member_accessibility_order";
        public const string CSharpMemberOrder = "pnfmt_csharp_member_order";
        public const string CSharpSortMembers = "pnfmt_csharp_sort_members";
        public const string CSharpSortMembersByName = "pnfmt_csharp_sort_members_by_name";
        public const string CSharpSortModifiers = "pnfmt_csharp_sort_modifiers";
        public const string CsProjEmptyLinesBetweenGroups = "pnfmt_csproj_empty_lines_between_groups";
        public const string CsProjSortItemTypes = "pnfmt_csproj_sort_item_types";
        public const string IniGroupByPrefix = "pnfmt_ini_group_by_prefix";
        public const string IniMergeGroups = "pnfmt_ini_merge_groups";
        public const string IniSortGroups = "pnfmt_ini_sort_groups";
        public const string ResxRemoveDocumentationComment = "pnfmt_resx_remove_documentation_comment";
        public const string ResxRemoveXsdSchema = "pnfmt_resx_remove_xsd_schema";
        public const string ResxSortComparer = "pnfmt_resx_sort_comparer";
        public const string SortEntries = "pnfmt_sort_entries";
        public const string XamlFormat = "pnfmt_xaml_format";
        public const string XmlFormat = "pnfmt_xml_format";
    }
}
