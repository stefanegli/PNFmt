// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;

namespace PNFmt
{
    internal static class EditorConfigFormatterActivation
    {
        public static bool? GetEnablement(IReadOnlyDictionary<string, string> settings)
        {
            if (!settings.TryGetValue(EditorConfigSettingNames.Enabled, out var value)
                || string.Equals(value, "unset", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return EditorConfigSettings.IsEnabled(value);
        }

        public static string GetSelection(IReadOnlyDictionary<string, string> settings)
        {
            return settings.TryGetValue(EditorConfigSettingNames.Formatter, out var value)
                && !string.Equals(value, "unset", StringComparison.OrdinalIgnoreCase)
                ? value
                : null;
        }

        public static bool IsEnabled(
            string targetFile,
            string formatterName,
            bool implicitlyEnabled,
            IFormatterLog log)
        {
            return IsEnabled(EditorConfigSettings.Load(targetFile), targetFile, formatterName, implicitlyEnabled, log);
        }

        public static bool IsEnabled(
            IReadOnlyDictionary<string, string> settings,
            string targetFile,
            string formatterName,
            bool implicitlyEnabled,
            IFormatterLog log)
        {
            var selectedFormatter = GetSelection(settings);
            var enabled = GetEnablement(settings);
            if (enabled == false || (selectedFormatter is not null
                && !string.Equals(selectedFormatter, formatterName, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var active = enabled == true || selectedFormatter is not null || implicitlyEnabled;
            if (active && (enabled is null || selectedFormatter is null))
            {
                log?.WriteLine($"{targetFile}: warning PNFMT004: Implicit formatter activation is deprecated. "
                    + $"Set 'pnfmt_enabled = true' and 'pnfmt_formatter = {formatterName}' in the applicable "
                    + ".editorconfig section to keep processing, or 'pnfmt_enabled = false' to disable it. "
                    + "Missing enablement or formatter selection will disable processing in a future version.");
            }

            return active;
        }
    }
}
