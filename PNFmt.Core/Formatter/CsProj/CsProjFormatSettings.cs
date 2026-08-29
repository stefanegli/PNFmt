// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace PNFmt
{
    internal interface ICsProjFormatSettings
    {
        int EmptyLinesBetweenGroups { get; }
        string EndOfLine { get; }
        char IndentStyle { get; }
        bool SortEntries { get; }
        System.Collections.Generic.IReadOnlyCollection<string> SortItemTypes { get; }
        int TabWidth { get; }
    }

    internal sealed class DefaultCsProjFormatSettings : ICsProjFormatSettings
    {
        public int EmptyLinesBetweenGroups => 1;
        public string EndOfLine => "\r\n";
        public char IndentStyle => ' ';
        public bool SortEntries => true;
        public System.Collections.Generic.IReadOnlyCollection<string> SortItemTypes => CsProjItemSorting.Defaults;
        public int TabWidth => 2;
    }
}
