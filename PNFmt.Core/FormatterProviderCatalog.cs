// Copyright (c) 2026 by Stefan Egli. All rights reserved.

namespace PNFmt
{
    public static class FormatterProviderCatalog
    {
        public static FormatterProviderRegistry CreateDefault()
        {
            return new FormatterProviderRegistry(
                new IFileFormatterProvider[]
                {
                    new CsProjFormatterProvider(),
                    new ResxFormatterProvider(),
                });
        }
    }
}

