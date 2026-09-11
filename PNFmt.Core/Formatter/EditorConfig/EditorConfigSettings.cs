// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;

namespace PNFmt
{
    internal static class EditorConfigSettings
    {
        private static readonly EditorConfig.Core.EditorConfigFileCache FileCache =
            new EditorConfig.Core.EditorConfigFileCache();

        public static IReadOnlyDictionary<string, string> Load(string targetFile)
        {
            try
            {
                // Share parsed files while resolving the hierarchy afresh on each
                // request, including newly created or removed child configurations.
                var parser = new EditorConfig.Core.EditorConfigParser(null, FileCache);
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
