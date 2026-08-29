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

        public bool TryGet(string settingName, string legacySettingName, out string value)
        {
            var hasSetting = this.properties.TryGetValue(settingName, out var settingValue);
            var hasLegacySetting = this.properties.TryGetValue(legacySettingName, out var legacySettingValue);

            if (hasLegacySetting)
            {
                var message = hasSetting
                    ? $"EditorConfig setting '{legacySettingName}' is deprecated and ignored because "
                        + $"'{settingName}' is set."
                    : $"EditorConfig setting '{legacySettingName}' is deprecated; use '{settingName}' instead.";
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
        public const string EmptyLinesBetweenGroups = "pnfmt_empty_lines_between_groups";
        public const string RemoveDocumentationComment = "pnfmt_remove_documentation_comment";
        public const string RemoveXsdSchema = "pnfmt_remove_xsd_schema";
        public const string SortComparer = "pnfmt_sort_comparer";
        public const string SortEntries = "pnfmt_sort_entries";
        public const string SortItemTypes = "pnfmt_sort_item_types";
    }
}
