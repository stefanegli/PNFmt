// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace PNFmt.Tests.Formatter.Xml
{
    public sealed class XamlFormatterTests
    {
        private const string Namespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        [Fact]
        public void Custom_containers_keep_their_own_whitespace_while_known_descendants_can_format()
        {
            const string Content = "<local:Inlines><local:Run/><local:Run/> <local:Run/></local:Inlines>";
            var result = Format("<local:View xmlns:local='clr-namespace:Example' xmlns='" + Namespace + "'>"
                + Content + "<Grid><Button/><Button/></Grid></local:View>");
            Assert.Contains(Content, result);
            Assert.Contains("</local:Inlines><Grid>\n    <Button/>\n    <Button/>\n  </Grid></local:View>", result);
            Assert.Equal(result, Format(result));
        }

        [Fact]
        public void Formats_layout_and_property_elements_without_loading_types()
        {
            var result = Format("<Grid xmlns='" + Namespace + "'><Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='*'/></Grid.RowDefinitions><Button Content='Hello'/></Grid>");
            Assert.Contains("\n  <Grid.RowDefinitions>\n    <RowDefinition Height='Auto'/>\n    <RowDefinition Height='*'/>\n  </Grid.RowDefinitions>\n  <Button Content='Hello'/>\n", result);
            Assert.Equal(result, Format(result));
        }

        [Theory]
        [InlineData("TextBlock")]
        [InlineData("Span")]
        [InlineData("Bold")]
        [InlineData("Italic")]
        [InlineData("Underline")]
        [InlineData("Hyperlink")]
        [InlineData("Paragraph")]
        [InlineData("TextBlock.Inlines")]
        [InlineData("FormattedString")]
        public void Inline_containers_never_gain_or_lose_text_separators(string name)
        {
            var content = "<" + name + "><Run Text='First'/><Run Text='Second'/> <Run Text='Third'/></" + name + ">";
            var result = Format("<Grid xmlns='" + Namespace + "'>" + content + "<Button/></Grid>");
            Assert.Contains(content, result);
            Assert.Contains("\n  <Button/>\n", result);
            Assert.Equal(result, Format(result));
        }

        [Fact]
        public void Mixed_text_and_xml_space_are_preserved()
        {
            const string Content = "<Grid xml:space='preserve'>\n<Button/> <Button/>\n</Grid>";
            const string Text = "<Button>Hello <Bold>world</Bold> !</Button>";
            var result = Format("<StackPanel xmlns='" + Namespace + "'>" + Content + Text + "</StackPanel>");
            Assert.Contains(Content, result);
            Assert.Contains(Text, result);
        }

        [Theory]
        [InlineData("https://github.com/avaloniaui")]
        [InlineData("http://schemas.microsoft.com/dotnet/2021/maui")]
        public void Recognizes_other_common_xaml_layout_namespaces(string namespaceUri)
        {
            Assert.Contains("\n  <Button/>\n", Format("<Grid xmlns='" + namespaceUri + "'><Button/></Grid>"));
        }

        [Fact]
        public void Resource_order_markup_extensions_attributes_and_namespaces_are_preserved()
        {
            const string First = "<SolidColorBrush x:Key='Zebra' Color='Blue'/>";
            const string Second = "<Style x:Key='Alpha' TargetType='{x:Type Button}'><Setter Property='Background' Value='{StaticResource Zebra}'/></Style>";
            var input = "<ResourceDictionary xmlns='" + Namespace + "' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>"
                + First + Second + "</ResourceDictionary>";
            var result = XmlDocumentFormatter.Format(input, new Dictionary<string, string> { ["pnfmt_sort_entries"] = "true" }, true);
            Assert.Contains(First, result);
            Assert.Contains("<Style x:Key='Alpha' TargetType='{x:Type Button}'>", result);
            Assert.Contains("<Setter Property='Background' Value='{StaticResource Zebra}'/>", result);
            Assert.True(result.IndexOf("x:Key='Zebra'") < result.IndexOf("x:Key='Alpha'"));
            Assert.Contains("xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'", result);
        }

        private static string Format(string text)
        {
            return XmlDocumentFormatter.Format(text, new Dictionary<string, string> { ["indent_size"] = "2" }, true);
        }
    }
}
