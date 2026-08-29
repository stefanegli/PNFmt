// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;

namespace PNFmt
{
    public sealed class FileFormatResult
    {
        public FileFormatResult(
            FileFormatStatus status,
            IReadOnlyList<FormatterDiagnostic> diagnostics = null)
        {
            this.Status = status;
            this.Diagnostics = diagnostics ?? Array.Empty<FormatterDiagnostic>();
        }

        public IReadOnlyList<FormatterDiagnostic> Diagnostics { get; }

        public FileFormatStatus Status { get; }
    }
}

