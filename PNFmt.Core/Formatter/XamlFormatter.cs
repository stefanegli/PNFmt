// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;

namespace PNFmt
{
    public sealed class XamlFormatter : IFileFormatter
    {
        private static readonly IReadOnlyCollection<string> Extensions = Array.AsReadOnly(new[] { ".xaml" });

        public IReadOnlyCollection<string> FileExtensions => Extensions;

        public string Name => "xaml";

        public FileFormatResult Format(FileFormatRequest request)
        {
            return XmlFileFormatting.Format(request, true);
        }
    }
}
