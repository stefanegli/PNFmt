// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace PNFmt
{
    internal interface ICsProjFormatSettings
    {
        int EmptyLinesBetweenGroups { get; }
        string EndOfLine { get; }
        int IndentSize { get; }
        char IndentStyle { get; }
        bool SortEntries { get; }
        System.Collections.Generic.IReadOnlyCollection<string> SortItemTypes { get; }
    }

    internal sealed class DefaultCsProjFormatSettings : ICsProjFormatSettings
    {
        public int EmptyLinesBetweenGroups => 1;
        public string EndOfLine => "\r\n";
        public int IndentSize => 2;
        public char IndentStyle => ' ';
        public bool SortEntries => true;
        public System.Collections.Generic.IReadOnlyCollection<string> SortItemTypes => CsProjItemSorting.Defaults;
    }
}
