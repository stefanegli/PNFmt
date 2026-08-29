// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using CsProjCore = global::CsProjFormatter;

namespace PNFmt
{
    public sealed class CsProjFormatterProvider : IFileFormatterProvider
    {
        private static readonly IReadOnlyCollection<string> Extensions =
            Array.AsReadOnly(new[] { ".csproj" });

        public IReadOnlyCollection<string> FileExtensions => Extensions;

        public string Name => "csproj";

        public FileFormatResult Format(FileFormatRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var formatter = new CsProjCore.ConfigurableCsProjFormatter(new LogAdapter(request.Log));
            formatter.Run(request.FilePath, request.WriteChanges, request.Lint);

            if (!formatter.IsActive || formatter.IsSkipped)
            {
                return new FileFormatResult(FileFormatStatus.Skipped, MapDiagnostics(formatter.Diagnostics));
            }

            var status = formatter.IsFileChanged
                ? FileFormatStatus.Updated
                : FileFormatStatus.Unchanged;
            return new FileFormatResult(status, MapDiagnostics(formatter.Diagnostics));
        }

        private static IReadOnlyList<FormatterDiagnostic> MapDiagnostics(
            IReadOnlyList<CsProjCore.FormatterDiagnostic> diagnostics)
        {
            return diagnostics
                .Select(diagnostic => new FormatterDiagnostic(
                    diagnostic.Code,
                    diagnostic.Message,
                    diagnostic.LineNumber))
                .ToArray();
        }

        private sealed class LogAdapter : CsProjCore.ILog
        {
            private readonly IFormatterLog log;

            public LogAdapter(IFormatterLog log)
            {
                this.log = log;
            }

            public void WriteLine(string message)
            {
                this.log.WriteLine(message);
            }
        }
    }
}

