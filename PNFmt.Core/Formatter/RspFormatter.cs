// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;

namespace PNFmt
{
    public sealed class RspFormatter : IFileFormatter
    {
        private static readonly IReadOnlyCollection<string> Extensions =
            Array.AsReadOnly(new[] { ".rsp" });

        public IReadOnlyCollection<string> FileExtensions => Extensions;

        public string Name => "rsp";

        public FileFormatResult Format(FileFormatRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return TextFileFormatPipeline.Format(
                request,
                EditorConfigFormatterActivation.IsEnabled(
                    request.FilePath,
                    EditorConfigSettingNames.SortEntries,
                    request.Log),
                RspDocumentFormatter.Format);
        }
    }
}
