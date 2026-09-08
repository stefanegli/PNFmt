// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;

namespace PNFmt
{
    internal static class XamlWhitespacePolicy
    {
        private static readonly HashSet<string> TextContainers = new HashSet<string>(StringComparer.Ordinal)
        {
            "TextBlock", "TextBox", "RichTextBox", "Span", "Run", "Bold", "Italic", "Underline", "Hyperlink",
            "Paragraph", "Section", "FlowDocument", "FormattedString", "String",
        };

        private static readonly HashSet<string> StructuralContainers = new HashSet<string>(StringComparer.Ordinal)
        {
            "Application", "Window", "Page", "UserControl", "ContentPage", "ContentView",
            "Grid", "StackPanel", "StackLayout", "VerticalStackLayout", "HorizontalStackLayout",
            "DockPanel", "WrapPanel", "Canvas", "UniformGrid", "Border", "Viewbox", "ScrollViewer",
            "ResourceDictionary", "Style", "Setter", "Trigger", "DataTrigger", "MultiTrigger", "MultiDataTrigger",
            "ControlTemplate", "DataTemplate", "ItemsPanelTemplate", "Storyboard", "VisualState", "VisualStateGroup",
        };

        private static readonly HashSet<string> StructuralProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "Resources", "Children", "RowDefinitions", "ColumnDefinitions", "MergedDictionaries",
            "Setters", "Triggers", "Conditions", "EnterActions", "ExitActions", "Template", "VisualStateGroups",
        };

        public static bool IsTextContainer(string localName)
        {
            return TextContainers.Contains(localName) || localName.EndsWith(".Inlines", StringComparison.Ordinal);
        }

        public static bool IsStructuralContainer(string namespaceUri, string localName)
        {
            if (namespaceUri != "http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                && namespaceUri != "https://github.com/avaloniaui"
                && namespaceUri != "http://schemas.microsoft.com/dotnet/2021/maui")
            {
                return false;
            }

            var separator = localName.IndexOf('.');
            return separator < 0 ? StructuralContainers.Contains(localName)
                : StructuralContainers.Contains(localName.Substring(0, separator))
                    && StructuralProperties.Contains(localName.Substring(separator + 1));
        }
    }
}
