// Copyright (c) 2026 by Stefan Egli. All rights reserved.

namespace PNFmt
{
    public static class FormatterCatalog
    {
        public static FormatterRegistry CreateDefault()
        {
            return new FormatterRegistry(
                new IFileFormatter[]
                {
                    new CSharpFormatter(),
                    new CsProjFormatter(),
                    new IniFormatter(),
                    new ResxFormatter(),
                    new RspFormatter(),
                    new SlnxFormatter(),
                    new XamlFormatter(),
                    new XmlFormatter(),
                });
        }
    }
}
