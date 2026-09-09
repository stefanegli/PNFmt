// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using System.Linq;
using System.Text;

namespace PNFmt
{
    internal static class TextFileFormatPipeline
    {
        public static FileFormatResult Format(
            FileFormatRequest request,
            bool isActive,
            Func<string, string> formatDocument,
            bool preserveEncoding = false,
            bool xml = false,
            Func<bool> shouldSkip = null)
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

            var encoding = FileEncoding.Load(request.FilePath, request.Log);
            var file = preserveEncoding || encoding is not null
                ? EncodedTextFile.Read(request.FilePath, encoding, xml && encoding is not null) : null;
            var original = file?.Text ?? File.ReadAllText(request.FilePath);
            var formatted = formatDocument(original);
            if (shouldSkip?.Invoke() == true)
            {
                return new FileFormatResult(FileFormatStatus.Skipped);
            }

            if (xml && encoding is not null)
            {
                formatted = FileEncoding.UpdateXmlDeclaration(formatted, encoding);
            }

            var bytes = encoding is not null ? FileEncoding.GetBytes(formatted, encoding) : null;
            var unchanged = bytes is not null
                ? File.ReadAllBytes(request.FilePath).SequenceEqual(bytes)
                : string.Equals(original, formatted, StringComparison.Ordinal);
            if (unchanged)
            {
                return new FileFormatResult(FileFormatStatus.Unchanged);
            }

            if (request.WriteChanges)
            {
                if (bytes is not null)
                {
                    File.WriteAllBytes(request.FilePath, bytes);
                }
                else if (file is not null)
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
