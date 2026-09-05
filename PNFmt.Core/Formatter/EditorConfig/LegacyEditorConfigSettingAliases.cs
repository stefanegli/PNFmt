// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;

namespace PNFmt
{
    internal sealed class EditorConfigSettingAlias
    {
        public EditorConfigSettingAlias(string currentName, string legacyName)
        {
            this.CurrentName = currentName;
            this.LegacyName = legacyName;
        }

        public string CurrentName { get; }

        public string LegacyName { get; }
    }

    internal static class LegacyEditorConfigSettingAliases
    {
        public static readonly EditorConfigSettingAlias CsProjSortEntries =
            new EditorConfigSettingAlias(
                EditorConfigSettingNames.SortEntries,
                "csproj_formatter_sort_entries");

        public static readonly EditorConfigSettingAlias CsProjEmptyLinesBetweenGroups =
            new EditorConfigSettingAlias(
                EditorConfigSettingNames.CsProjEmptyLinesBetweenGroups,
                "csproj_formatter_empty_lines_between_groups");

        public static readonly EditorConfigSettingAlias CsProjSortItemTypes =
            new EditorConfigSettingAlias(
                EditorConfigSettingNames.CsProjSortItemTypes,
                "csproj_formatter_sort_item_types");

        public static readonly EditorConfigSettingAlias ResxSortEntries =
            new EditorConfigSettingAlias(
                EditorConfigSettingNames.SortEntries,
                "resx_formatter_sort_entries");

        public static readonly EditorConfigSettingAlias ResxRemoveXsdSchema =
            new EditorConfigSettingAlias(
                EditorConfigSettingNames.ResxRemoveXsdSchema,
                "resx_formatter_remove_xsd_schema");

        public static readonly EditorConfigSettingAlias ResxRemoveDocumentationComment =
            new EditorConfigSettingAlias(
                EditorConfigSettingNames.ResxRemoveDocumentationComment,
                "resx_formatter_remove_documentation_comment");

        public static readonly EditorConfigSettingAlias ResxSortComparer =
            new EditorConfigSettingAlias(
                EditorConfigSettingNames.ResxSortComparer,
                "resx_formatter_sort_comparer");

        public static readonly IReadOnlyCollection<EditorConfigSettingAlias> All =
            Array.AsReadOnly(new[]
            {
                CsProjSortEntries,
                CsProjEmptyLinesBetweenGroups,
                CsProjSortItemTypes,
                ResxSortEntries,
                ResxRemoveXsdSchema,
                ResxRemoveDocumentationComment,
                ResxSortComparer,
            });
    }
}
