namespace PNFmt.Tests.Formatter.Resx.Fake
{
    using System;

    using PNFmt;

    internal sealed class FakeSettings : IResxFormatSettings
    {
        public StringComparer Comparer { get; set; } = StringComparer.Ordinal;
        public ResxLayoutSettings Layout { get; set; }
        public bool InsertDocumentationComment { get; set; } = true;
        public bool InsertXsdSchema { get; set; } = true;
        public bool RemoveDocumentationComment { get; set; }
        public bool RemoveXsdSchema { get; set; }
        public bool SortEntries { get; set; }
    }
}
