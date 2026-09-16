// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Xml;

namespace PNFmt
{
    internal static class XmlFileFormatting
    {
        public static FileFormatResult Format(FileFormatRequest request, bool xaml)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            request = request.ResolveConfiguration();
            var settings = request.Configuration.Properties;
            return TextFileFormatPipeline.Format(request,
                request.Configuration.IsActive(xaml ? "xaml" : "xml"),
                (text, _) =>
                {
                    if (!request.Configuration.FormatLayout(xaml ? "xaml" : "xml"))
                    {
                        return DocumentFormatResult.FromText(text);
                    }

                    try
                    {
                        return DocumentFormatResult.FromText(XmlDocumentFormatter.Format(text, settings, xaml));
                    }
                    catch (XmlException exception)
                    {
                        var diagnostic = new FormatterDiagnostic(xaml ? "XAML001" : "XML001",
                            "Formatting skipped: " + exception.Message,
                            exception.LineNumber > 0 ? (int?)exception.LineNumber : null);
                        return DocumentFormatResult.Skipped(diagnostic);
                    }
                },
                preserveEncoding: true,
                xml: true);
        }
    }
}
