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

            var settings = EditorConfigSettings.Load(request.FilePath);
            return TextFileFormatPipeline.Format(
                request,
                EditorConfigFormatterActivation.IsEnabled(
                    settings, request.FilePath, this.Name,
                    EditorConfigSettings.IsEnabled(settings, EditorConfigSettingNames.CSharpFormat), request.Log),
                (text, _) =>
                {
                    if (CSharpGeneratedCode.IsGenerated(request.FilePath, text, settings))
                    {
                        request.Log.WriteLine($"Skipping generated C# file {request.FilePath}.");
                        return DocumentFormatResult.Skipped();
                    }

                    var formatted = CSharpDocumentFormatter.Format(text, settings, out var diagnostic);
                    return diagnostic is null
                        ? DocumentFormatResult.FromText(formatted)
                        : DocumentFormatResult.Skipped(diagnostic);
                },
                preserveEncoding: true);
        }
    }
}
