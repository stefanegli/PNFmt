namespace PNFmt
{
    using System;

    internal interface IResxFormatSettings
    {
        StringComparer Comparer { get; }
        ResxLayoutSettings Layout { get; }
        bool InsertDocumentationComment { get; }
        bool InsertXsdSchema { get; }
        bool RemoveDocumentationComment { get; }
        bool RemoveXsdSchema { get; }
        bool SortEntries { get; }
    }
}
