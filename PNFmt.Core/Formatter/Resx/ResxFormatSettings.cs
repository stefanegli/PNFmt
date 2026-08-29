namespace PNFmt
{
    using System;

    internal interface IResxFormatSettings
    {
        StringComparer Comparer { get; }
        bool RemoveDocumentationComment { get; }
        bool RemoveXsdSchema { get; }
        bool SortEntries { get; }
    }
}
