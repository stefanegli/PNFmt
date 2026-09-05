// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PNFmt
{
    internal static class EditorConfigSettings
    {
        private static readonly IReadOnlyDictionary<string, string> Empty =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        public static IReadOnlyDictionary<string, string> Load(
            string targetFile,
            IFormatterLog log)
        {
            try
            {
                var parser = new EditorConfig.Core.EditorConfigParser();
                return parser.Parse(targetFile).Properties;
            }
            catch (Exception ex)
            {
                log?.WriteLine("Failed to parse EditorConfig file:\n" + ex.ToString());
                return Empty;
            }
        }

        public static bool IsEnabled(
            IReadOnlyDictionary<string, string> settings,
            string settingName)
        {
            return settings.TryGetValue(settingName, out var value)
                && IsEnabled(value);
        }

        public static bool IsEnabled(string value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
