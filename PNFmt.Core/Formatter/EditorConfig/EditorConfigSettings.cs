// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;

namespace PNFmt
{
    internal static class EditorConfigSettings
    {
        public static IReadOnlyDictionary<string, string> Load(string targetFile)
        {
            try
            {
                var parser = new EditorConfig.Core.EditorConfigParser(
                    EditorConfig.Core.EditorConfigFileCache.GetOrCreate,
                    null,
                    null);
                return parser.Parse(targetFile).Properties;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    $"Unable to read EditorConfig settings for '{targetFile}': {ex.Message}", ex);
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
