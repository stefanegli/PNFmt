// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;

namespace PNFmt
{
    // A document either supplies text, supplies bytes from its own serializer,
    // or rejects formatting. Only the file pipeline decides whether to write.
    internal sealed class DocumentFormatResult
    {
        private DocumentFormatResult(string text, byte[] bytes, IReadOnlyList<FormatterDiagnostic> diagnostics)
        {
            this.Text = text;
            this.Bytes = bytes;
            this.Diagnostics = diagnostics ?? Array.Empty<FormatterDiagnostic>();
        }

        public string Text { get; }
        public byte[] Bytes { get; }
        public IReadOnlyList<FormatterDiagnostic> Diagnostics { get; }
        public bool IsSkipped => this.Text is null && this.Bytes is null;

        public static DocumentFormatResult FromText(string text, IReadOnlyList<FormatterDiagnostic> diagnostics = null)
            => new DocumentFormatResult(text ?? throw new ArgumentNullException(nameof(text)), null, diagnostics);

        public static DocumentFormatResult FromBytes(byte[] bytes)
            => new DocumentFormatResult(null, bytes ?? throw new ArgumentNullException(nameof(bytes)), null);

        public static DocumentFormatResult Skipped(params FormatterDiagnostic[] diagnostics)
            => new DocumentFormatResult(null, null, diagnostics);
    }
}
