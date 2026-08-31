// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;

namespace PNFmt
{
    internal static class EditorConfigFormatterActivation
    {
        public static bool IsEnabled(string targetFile, string settingName, IFormatterLog log)
        {
            if (string.IsNullOrWhiteSpace(targetFile))
            {
                throw new ArgumentException("A target file is required.", nameof(targetFile));
            }

            if (string.IsNullOrWhiteSpace(settingName))
            {
                throw new ArgumentException("A setting name is required.", nameof(settingName));
            }

            try
            {
                var parser = new EditorConfig.Core.EditorConfigParser();
                var properties = parser.Parse(targetFile).Properties;
                return properties.TryGetValue(settingName, out var value)
                    && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                log?.WriteLine("Failed to parse EditorConfig file:\n" + ex.ToString());
                return false;
            }
        }
    }
}
