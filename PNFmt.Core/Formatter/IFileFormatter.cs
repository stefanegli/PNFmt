// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System.Collections.Generic;

namespace PNFmt
{
    public interface IFileFormatter
    {
        IReadOnlyCollection<string> FileExtensions { get; }

        string Name { get; }

        FileFormatResult Format(FileFormatRequest request);
    }
}
