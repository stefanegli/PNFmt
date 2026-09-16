// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;

namespace PNFmt
{
    internal static class EditorConfigFormatterOptions
    {
        public static bool Format(IReadOnlyDictionary<string, string> settings, string formatterName)
        {
            if (TryGet(settings, EditorConfigSettingNames.Format, out var format))
            {
                return EditorConfigSettings.IsEnabled(format);
            }

            // Keep the existing C#, XML, and XAML switches as layout options.
            var legacyName = formatterName == "csharp" ? EditorConfigSettingNames.CSharpFormat
                : formatterName == "xml" ? EditorConfigSettingNames.XmlFormat
                : formatterName == "xaml" ? EditorConfigSettingNames.XamlFormat
                : null;
            return legacyName is null || !TryGet(settings, legacyName, out format)
                || EditorConfigSettings.IsEnabled(format);
        }

        private static bool TryGet(IReadOnlyDictionary<string, string> settings, string name, out string value)
        {
            return settings.TryGetValue(name, out value)
                && !string.Equals(value, "unset", StringComparison.OrdinalIgnoreCase);
        }
    }
}
