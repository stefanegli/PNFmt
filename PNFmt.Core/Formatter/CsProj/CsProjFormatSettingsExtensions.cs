// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace PNFmt
{
    internal static class CsProjFormatSettingsExtensions
    {
        public static string ResolveIndentChars(this ICsProjFormatSettings settings)
        {
            if (settings.IndentStyle == '\t')
            {
                return "\t";
            }

            var width = settings.TabWidth > 0 ? settings.TabWidth : 2;
            return new string(settings.IndentStyle, width);
        }

        public static string ResolveNewLineChars(this ICsProjFormatSettings settings)
        {
            return string.IsNullOrEmpty(settings.EndOfLine) ? "\r\n" : settings.EndOfLine;
        }
    }
}
