// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace PNFmt.Tests.Formatter.CsProj
{
    using PNFmt;
    using System;

    internal sealed class FakeLog : IFormatterLog
    {
        public void Write(Exception exception)
        {
        }

        public void WriteLine(string message)
        {
        }
    }
}
