// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;

namespace PNFmt
{
    public sealed class IniFormatter : IFileFormatter
    {
        private static readonly IReadOnlyCollection<string> Extensions =
            Array.AsReadOnly(new[] { ".editorconfig", ".ini" });

        public IReadOnlyCollection<string> FileExtensions => Extensions;

        public string Name => "ini";

        public FileFormatResult Format(FileFormatRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var settings = new IniEditorConfigSettings(request.FilePath, request.Log);
            return TextFileFormatPipeline.Format(
                request,
                settings.IsActive,
                text => IniDocumentFormatter.Format(
                    text,
                    settings.SortEntries,
                    settings.SortGroups,
                    settings.GroupByPrefix));
        }
    }
}
