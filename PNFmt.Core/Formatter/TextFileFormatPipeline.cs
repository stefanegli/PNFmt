// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
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
            bool xml = false)
        {
            if (formatDocument is null)
            {
                throw new ArgumentNullException(nameof(formatDocument));
            }

            return Format(request, isActive,
                (text, _) => DocumentFormatResult.FromText(formatDocument(text)), preserveEncoding, xml);
        }

        public static FileFormatResult Format(
            FileFormatRequest request,
            bool isActive,
            Func<string, Encoding, DocumentFormatResult> formatDocument,
            bool preserveEncoding = false,
            bool xml = false)
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

            request = request.ResolveConfiguration();
            var encoding = request.Configuration.Encoding;
            // Decode strictly even when the formatter's default output is UTF-8.
            // XML declarations identify the input independently of output settings.
            var file = EncodedTextFile.Read(request.FilePath, encoding, xml);
            var result = formatDocument(file.Text, encoding);
            if (result.IsSkipped)
            {
                return new FileFormatResult(FileFormatStatus.Skipped, result.Diagnostics);
            }

            var formatted = result.Text;
            var bytes = result.Bytes;
            if (bytes is null && encoding is not null)
            {
                if (xml)
                {
                    formatted = FileEncoding.UpdateXmlDeclaration(formatted, encoding);
                }

                bytes = FileEncoding.GetBytes(formatted, encoding);
            }

            // With no explicit encoding, retain each formatter's existing rule:
            // unchanged text alone must not normalize the original encoding/BOM.
            var unchanged = bytes is not null
                ? file.HasSameBytes(bytes)
                : string.Equals(file.Text, formatted, StringComparison.Ordinal);
            if (unchanged)
            {
                return new FileFormatResult(FileFormatStatus.Unchanged, result.Diagnostics);
            }

            // Encoding failures must occur during preview too, before opening a
            // destination for writing. Format-specific serializers also finish first.
            bytes ??= preserveEncoding
                ? file.GetBytes(formatted)
                : new UTF8Encoding(false, true).GetBytes(formatted);
            if (request.WriteChanges)
            {
                file.Write(request.FilePath, bytes);
            }

            request.Log.Progress(
                $"{(request.WriteChanges ? "Updating" : "Would update")} {request.FilePath}");
            return new FileFormatResult(FileFormatStatus.Updated, result.Diagnostics);
        }
    }
}
