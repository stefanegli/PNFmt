// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PNFmt
{
    internal static class EditorConfigSettings
    {
        private static readonly EditorConfig.Core.EditorConfigFileCache FileCache =
            new EditorConfig.Core.EditorConfigFileCache();

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

        public static IReadOnlyDictionary<string, string> Load(string targetFile)
        {
            try
            {
                // Share parsed files while resolving the hierarchy afresh on each
                // request, including newly created or removed child configurations.
                var parser = new EditorConfig.Core.EditorConfigParser(null, FileCache);
                var properties = parser.Parse(targetFile).Properties;
                // The parser resolves hierarchy but retains the literal unset value.
                if (properties.Values.Contains("unset", StringComparer.OrdinalIgnoreCase))
                {
                    return properties.Where(pair => !string.Equals(pair.Value, "unset", StringComparison.OrdinalIgnoreCase))
                        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                }
                return properties;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    $"Unable to read EditorConfig settings for '{targetFile}': {ex.Message}", ex);
            }
        }
    }
}
