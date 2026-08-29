// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace CsProjFormatter
{
    public interface ISettings
    {
        int EmptyLinesBetweenGroups { get; }
        string EndOfLine { get; }
        char IndentStyle { get; }
        bool SortEntries { get; }
        int TabWidth { get; }
    }

    public class Settings : ISettings, IItemSortingSettings
    {
        public int EmptyLinesBetweenGroups => 1;
        public string EndOfLine => "\r\n";
        public char IndentStyle => ' ';
        public bool SortEntries => true;
        public System.Collections.Generic.IReadOnlyCollection<string> SortItemTypes => ItemSortingSettings.Defaults;
        public int TabWidth => 2;
    }
}
