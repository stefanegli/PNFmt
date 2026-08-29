// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;

namespace PNFmt
{
    public sealed class ResxFormatter : IFileFormatter
    {
        private static readonly IReadOnlyCollection<string> Extensions =
            Array.AsReadOnly(new[] { ".resx" });

        public IReadOnlyCollection<string> FileExtensions => Extensions;

        public string Name => "resx";

        public FileFormatResult Format(FileFormatRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var settings = new ResxEditorConfigSettings(request.Log, request.FilePath);

            if (!settings.IsActive)
            {
                return new FileFormatResult(FileFormatStatus.Skipped);
            }

            var formatter = new ResxDocumentFormatter(settings, request.Log);
            formatter.Run(request.FilePath, request.WriteChanges);
            var status = formatter.IsFileChanged
                ? FileFormatStatus.Updated
                : FileFormatStatus.Unchanged;
            return new FileFormatResult(status);
        }
    }
}
