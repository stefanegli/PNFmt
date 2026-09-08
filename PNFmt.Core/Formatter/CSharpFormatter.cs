// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;

namespace PNFmt
{
    public sealed class CSharpFormatter : IFileFormatter
    {
        private static readonly IReadOnlyCollection<string> Extensions =
            Array.AsReadOnly(new[] { ".cs" });

        public IReadOnlyCollection<string> FileExtensions => Extensions;

        public string Name => "csharp";

        public FileFormatResult Format(FileFormatRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var settings = EditorConfigSettings.Load(request.FilePath, request.Log);
            FormatterDiagnostic diagnostic = null;
            var generated = false;
            var result = TextFileFormatPipeline.Format(
                request,
                EditorConfigSettings.IsEnabled(settings, EditorConfigSettingNames.CSharpFormat),
                text =>
                {
                    generated = CSharpGeneratedCode.IsGenerated(request.FilePath, text, settings);
                    return generated ? text : CSharpDocumentFormatter.Format(text, settings, out diagnostic);
                },
                preserveEncoding: true);
            if (generated)
            {
                request.Log.WriteLine($"Skipping generated C# file {request.FilePath}.");
                return new FileFormatResult(FileFormatStatus.Skipped);
            }

            return diagnostic is null
                ? result
                : new FileFormatResult(FileFormatStatus.Skipped, new[] { diagnostic });
        }
    }
}
