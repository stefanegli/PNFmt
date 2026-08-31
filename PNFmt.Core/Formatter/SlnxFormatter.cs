// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PNFmt
{
    public sealed class SlnxFormatter : IFileFormatter
    {
        private static readonly IReadOnlyCollection<string> Extensions =
            Array.AsReadOnly(new[] { ".slnx" });

        public IReadOnlyCollection<string> FileExtensions => Extensions;

        public string Name => "slnx";

        public FileFormatResult Format(FileFormatRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!EditorConfigFormatterActivation.IsEnabled(
                request.FilePath,
                EditorConfigSettingNames.SortEntries,
                request.Log))
            {
                return new FileFormatResult(FileFormatStatus.Skipped);
            }

            var original = File.ReadAllText(request.FilePath);
            var formatted = SlnxDocumentFormatter.Format(original);
            if (string.Equals(original, formatted, StringComparison.Ordinal))
            {
                return new FileFormatResult(FileFormatStatus.Unchanged);
            }

            if (request.WriteChanges)
            {
                File.WriteAllText(request.FilePath, formatted, new UTF8Encoding(false));
            }

            request.Log.WriteLine(
                $"{(request.WriteChanges ? "Updating" : "Would update")} {request.FilePath}");
            return new FileFormatResult(FileFormatStatus.Updated);
        }
    }
}
