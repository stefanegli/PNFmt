// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;

namespace PNFmt
{
    internal static class EditorConfigFormatterActivation
    {
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
            if (selectedFormatter is not null)
            {
                return string.Equals(selectedFormatter, formatterName, StringComparison.OrdinalIgnoreCase);
            }

            if (implicitlyEnabled)
            {
                log?.WriteLine($"{targetFile}: warning PNFMT004: Implicit formatter activation is deprecated. "
                    + $"Set 'pnfmt_formatter = {formatterName}' in the applicable .editorconfig section "
                    + "to keep formatting, or 'pnfmt_formatter = None' to disable it. "
                    + "A missing pnfmt_formatter setting will disable formatting in a future version.");
            }

            return implicitlyEnabled;
        }
    }
}
