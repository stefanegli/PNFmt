// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;

namespace PNFmt.Tests
{
    internal sealed class NullFormatterLog : IFormatterLog
    {
        public static NullFormatterLog Instance { get; } = new NullFormatterLog();

        private NullFormatterLog() { }

        public void Write(Exception exception) { }

        public void WriteLine(string message) { }
    }
}
