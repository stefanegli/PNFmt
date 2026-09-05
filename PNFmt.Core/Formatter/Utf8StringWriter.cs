// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System.IO;
using System.Text;

namespace PNFmt
{
    internal sealed class Utf8StringWriter : StringWriter
    {
        private static readonly Encoding Utf8Encoding = new UTF8Encoding(false);

        public override Encoding Encoding => Utf8Encoding;
    }
}
