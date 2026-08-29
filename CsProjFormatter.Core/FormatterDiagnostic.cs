// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace CsProjFormatter
{
    public sealed class FormatterDiagnostic
    {
        public FormatterDiagnostic(string code, string message, int? lineNumber)
        {
            this.Code = code;
            this.Message = message;
            this.LineNumber = lineNumber;
        }

        public string Code { get; }

        public int? LineNumber { get; }

        public string Message { get; }
    }
}
