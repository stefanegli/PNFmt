// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using System.Text;

namespace PNFmt
{
    internal static class TextFileFormatPipeline
    {
        public static FileFormatResult Format(
            FileFormatRequest request,
            bool isActive,
            Func<string, string> formatDocument,
            bool preserveEncoding = false)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (formatDocument is null)
            {
                throw new ArgumentNullException(nameof(formatDocument));
            }

            if (!isActive)
            {
                return new FileFormatResult(FileFormatStatus.Skipped);
            }

            var file = preserveEncoding ? EncodedTextFile.Read(request.FilePath) : null;
            var original = file?.Text ?? File.ReadAllText(request.FilePath);
            var formatted = formatDocument(original);
            if (string.Equals(original, formatted, StringComparison.Ordinal))
            {
                return new FileFormatResult(FileFormatStatus.Unchanged);
            }

            if (request.WriteChanges)
            {
                if (file is not null)
                {
                    file.Write(request.FilePath, formatted);
                }
                else
                {
                    File.WriteAllText(request.FilePath, formatted, new UTF8Encoding(false));
                }
            }

            request.Log.WriteLine(
                $"{(request.WriteChanges ? "Updating" : "Would update")} {request.FilePath}");
            return new FileFormatResult(FileFormatStatus.Updated);
        }
    }
}
