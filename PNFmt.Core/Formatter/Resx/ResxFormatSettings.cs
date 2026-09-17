namespace PNFmt
{
    using System;

    internal interface IResxFormatSettings
    {
        StringComparer Comparer { get; }
        bool InsertDocumentationComment { get; }
        bool InsertXsdSchema { get; }
        ResxLayoutSettings Layout { get; }
        bool RemoveDocumentationComment { get; }
        bool RemoveXsdSchema { get; }
        bool SortEntries { get; }
    }
}
