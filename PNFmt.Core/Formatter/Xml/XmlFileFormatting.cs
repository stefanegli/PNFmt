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

            var settings = EditorConfigSettings.Load(request.FilePath);
            FormatterDiagnostic diagnostic = null;
            var result = TextFileFormatPipeline.Format(request,
                EditorConfigFormatterActivation.IsEnabled(
                    settings, request.FilePath, xaml ? "xaml" : "xml",
                    EditorConfigSettings.IsEnabled(settings, activationSetting), request.Log),
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
                preserveEncoding: true,
                xml: true,
                shouldSkip: () => diagnostic is not null);
            return diagnostic is null ? result : new FileFormatResult(FileFormatStatus.Skipped, new[] { diagnostic });
        }
    }
}
