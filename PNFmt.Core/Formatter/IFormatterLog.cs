// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;

namespace PNFmt
{
    public interface IFormatterLog
    {
        void Write(Exception exception);

        void WriteLine(string message);
    }
}

