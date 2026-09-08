// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Xml;

namespace PNFmt
{
    internal static class XmlFileFormatting
    {
        public static FileFormatResult Format(FileFormatRequest request, string activationSetting, bool xaml)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var settings = EditorConfigSettings.Load(request.FilePath, request.Log);
            FormatterDiagnostic diagnostic = null;
            var result = TextFileFormatPipeline.Format(request,
                EditorConfigSettings.IsEnabled(settings, activationSetting),
                text =>
                {
                    try
                    {
                        return XmlDocumentFormatter.Format(text, settings, xaml);
                    }
                    catch (XmlException exception)
                    {
                        diagnostic = new FormatterDiagnostic(xaml ? "XAML001" : "XML001",
                            "Formatting skipped: " + exception.Message,
                            exception.LineNumber > 0 ? (int?)exception.LineNumber : null);
                        return text;
                    }
                },
                preserveEncoding: true);
            return diagnostic is null ? result : new FileFormatResult(FileFormatStatus.Skipped, new[] { diagnostic });
        }
    }
}
