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

            request = request.ResolveConfiguration();
            var settings = request.Configuration.ResourceSettings;

            if (!request.Configuration.IsActive(this.Name))
            {
                return new FileFormatResult(FileFormatStatus.Skipped);
            }

            return new ResxDocumentFormatter(settings).Run(request, settings.FormatLayout, settings.HasExplicitLayout);
        }
    }
}
