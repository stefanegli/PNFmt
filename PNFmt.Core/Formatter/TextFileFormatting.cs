// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;

namespace PNFmt
{
    internal static class TextFileFormatting
    {
        public static string DetectNewLine(string text)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (text.IndexOf("\r\n", StringComparison.Ordinal) >= 0)
            {
                return "\r\n";
            }

            if (text.IndexOf('\r') >= 0)
            {
                return "\r";
            }

            return "\n";
        }
    }
}
