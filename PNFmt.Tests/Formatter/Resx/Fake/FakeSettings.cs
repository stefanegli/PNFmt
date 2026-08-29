namespace PNFmt.Tests.Formatter.Resx.Fake
{
    using PNFmt;

    using System;

    internal sealed class FakeSettings : IResxFormatSettings
    {
        public StringComparer Comparer { get; set; } = StringComparer.Ordinal;
        public bool RemoveDocumentationComment { get; set; }
        public bool RemoveXsdSchema { get; set; }
        public bool SortEntries { get; set; }
    }
}
