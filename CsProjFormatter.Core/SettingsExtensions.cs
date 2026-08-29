// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace CsProjFormatter
{
    public static class SettingsExtensions
    {
        public static string ResolveIndentChars(this ISettings settings)
        {
            if (settings.IndentStyle == '\t')
            {
                return "\t";
            }

            var width = settings.TabWidth > 0 ? settings.TabWidth : 2;
            return new string(settings.IndentStyle, width);
        }

        public static string ResolveNewLineChars(this ISettings settings)
        {
            return string.IsNullOrEmpty(settings.EndOfLine) ? "\r\n" : settings.EndOfLine;
        }
    }
}