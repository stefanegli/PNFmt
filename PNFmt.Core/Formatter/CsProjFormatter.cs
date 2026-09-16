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

            request = request.ResolveConfiguration();
            var configuration = request.Configuration;
            if (!configuration.IsActive(this.Name, request.Lint))
            {
                return new FileFormatResult(FileFormatStatus.Skipped);
            }

            var formatter = new CsProjDocumentFormatter(configuration.ProjectSettings);
            return formatter.Run(
                request,
                configuration.FormatLayout(this.Name));
        }
    }
}
