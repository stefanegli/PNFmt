// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;

namespace PNFmt
{
    public sealed class CsProjFormatter : IFileFormatter
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

            var settings = new CsProjEditorConfigSettings(request.FilePath, request.Log);
            var isActive = settings.IsActive || request.Lint;

            if (!isActive)
            {
                return new FileFormatResult(FileFormatStatus.Skipped);
            }

            ICsProjFormatSettings effectiveSettings = settings.IsActive
                ? settings
                : new DefaultCsProjFormatSettings();
            var formatter = new CsProjDocumentFormatter(effectiveSettings, request.Log);
            var runResult = formatter.RunWithResult(request.FilePath, request.WriteChanges);
            if (runResult == CsProjFormatResult.SkippedNonSdkStyle)
            {
                return new FileFormatResult(FileFormatStatus.Skipped, formatter.Diagnostics);
            }

            var status = runResult == CsProjFormatResult.Updated
                ? FileFormatStatus.Updated
                : FileFormatStatus.Unchanged;
            return new FileFormatResult(status, formatter.Diagnostics);
        }
    }
}
