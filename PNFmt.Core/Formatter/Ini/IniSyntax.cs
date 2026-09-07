// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;

namespace PNFmt
{
    internal static class IniSyntax
    {
        public static bool IsSectionHeader(string line)
        {
            var trimmed = line.Trim();
            return trimmed.Length >= 2
                && trimmed[0] == '['
                && trimmed[trimmed.Length - 1] == ']';
        }

        public static bool TryParseProperty(
            string line,
            out IniProperty property,
            bool allowColon = false)
        {
            property = null;
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0
                || trimmed[0] == '#'
                || trimmed[0] == ';'
                || IsSectionHeader(trimmed))
            {
                return false;
            }

            var equalsIndex = line.IndexOf('=');
            var colonIndex = allowColon ? line.IndexOf(':') : -1;
            var separatorIndex = equalsIndex < 0
                ? colonIndex
                : colonIndex < 0 ? equalsIndex : Math.Min(equalsIndex, colonIndex);
            if (separatorIndex <= 0)
            {
                return false;
            }

            var key = line.Substring(0, separatorIndex).Trim();
            if (key.Length == 0)
            {
                return false;
            }

            property = new IniProperty(
                key,
                line.Substring(separatorIndex + 1).Trim(),
                separatorIndex);
            return true;
        }
    }

    internal sealed class IniProperty
    {
        public IniProperty(string key, string value, int separatorIndex)
        {
            this.Key = key;
            this.Value = value;
            this.SeparatorIndex = separatorIndex;
        }

        public string Formatted => $"{this.Key} = {this.Value}";

        public string Key { get; }

        public int SeparatorIndex { get; }

        public string Value { get; }
    }
}
