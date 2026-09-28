// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace PNFmt.Tests.Formatter.CSharp
{
    public sealed class CSharpInitializerWrappingTests
    {
        [Fact]
        public void Comments_directives_literals_and_exclusions_protect_their_boundaries()
        {
            const string Protected = "// pnfmt: off\nint[] off=[1,2];\n// pnfmt: on\n";
            const string Inactive = "#if NEVER\nint[] inactive=[1,2];\n#endif\n";
            var result = Format(Protected + Inactive
                + "int[] x = [first, /* attached */ second, third,];\n"
                + "int[] y = [first, // attached\nsecond, third];\n"
                + "int[] z = [first,\n#if DEBUG\nsecond,\n#endif\nthird];\n"
                + "string[] text = [\"a long literal that must never be split\", $\"{new[] { 1, 2 }}\"];",
                ("csharp_wrap_list_pattern", "chop_always"), ("csharp_wrap_array_initializer_style", "chop_always"),
                ("max_line_length", "10"));
            Assert.StartsWith(Protected + Inactive, result);
            Assert.Contains("first, /* attached */ second,", result);
            Assert.Contains("first, // attached\n", result);
            Assert.Contains("#if DEBUG\nsecond,\n#endif\n", result);
            Assert.Contains("\"a long literal that must never be split\"", result);
            Assert.Contains("$\"{new[] { 1, 2 }}\"", result);
        }

        [Theory]
        [InlineData("pnfmt_format")]
        [InlineData("pnfmt_csharp_format")]
        public void Disabled_layout_prevents_all_initializer_edits(string layout)
        {
            const string Input = "int[] x=[1,2];var y=new[]{1,2};var z=new C{A=1,B=2};";
            Assert.Equal(Input, Format(Input, (layout, "false"), ("csharp_wrap_list_pattern", "chop_always"),
                ("csharp_wrap_array_initializer_style", "chop_always"),
                ("csharp_wrap_object_and_collection_initializer_style", "chop_always")));
        }

        [Theory]
        [InlineData("int[] x = [];", "csharp_wrap_list_pattern")]
        [InlineData("var x = new int[] { };", "csharp_wrap_array_initializer_style")]
        [InlineData("var x = new C { };", "csharp_wrap_object_and_collection_initializer_style")]
        [InlineData("var x = values is [];", "csharp_wrap_list_pattern")]
        public void Empty_lists_stay_compact(string input, string setting)
        {
            Assert.Equal(input, Format(input, (setting, "chop_always")));
        }

        [Fact]
        public void Nested_arrays_spreads_dictionary_entries_and_with_initializers_keep_their_syntax()
        {
            var result = Format("int[][] arrays = [[1, 2], .. other];\n"
                + "var matrix = new[,] { { 1, 2 }, { 3, 4 } };\n"
                + "var map = new Dictionary<int, int> { { 1, 2 }, { 3, 4 } };\n"
                + "var changed = original with { A = 1, B = 2 };",
                ("csharp_wrap_list_pattern", "chop_always"), ("csharp_wrap_array_initializer_style", "chop_always"),
                ("csharp_wrap_object_and_collection_initializer_style", "chop_always"));
            Assert.Contains(".. other", result);
            Assert.Contains("{ 1, 2 },", result);
            Assert.Contains("A = 1,\n    B = 2", result);
        }

        [Fact]
        public void Roslyn_brace_placement_still_controls_initializer_opening_braces()
        {
            Assert.Contains("new C {\n", Format("var x = new C { A = 1, B = 2 };",
                ("csharp_wrap_object_and_collection_initializer_style", "chop_always"),
                ("csharp_new_line_before_open_brace", "none")));
        }

        [Theory]
        [InlineData("int[] x = [1,];", "csharp_wrap_list_pattern")]
        [InlineData("var x = new[] { 1, };", "csharp_wrap_array_initializer_style")]
        [InlineData("var x = new C { A = 1, };", "csharp_wrap_object_and_collection_initializer_style")]
        public void Single_items_trailing_commas_and_physical_newlines_are_preserved(string input, string setting)
        {
            var result = Format(input, (setting, "chop_always"), ("indent_style", "tab"),
                ("indent_size", "tab"), ("tab_width", "4"), ("end_of_line", "crlf"));
            Assert.Contains("\r\n\t", result);
            Assert.Contains(",\r\n", result);
            Assert.DoesNotContain("\n", result.Replace("\r\n", ""));
        }

        [Theory]
        [InlineData("csharp_wrap_array_initializer_style", "var x = new[] { 1, 2 };", "1,\n    2")]
        [InlineData("csharp_wrap_object_and_collection_initializer_style", "var x = new C { A = 1, B = 2 };", "A = 1,\n    B = 2")]
        [InlineData("csharp_wrap_list_pattern", "int[] x = [1, 2];", "1,\n    2")]
        public void Styles_and_aliases_are_opt_in_and_follow_existing_precedence(string setting, string input, string elements)
        {
            var expected = Format(input, (setting, "chop_always"));
            Assert.Contains(elements, expected);
            Assert.Equal(expected, Format(input, ("resharper_" + setting, "CHOP_ALWAYS")));
            Assert.Equal(expected, Format(input, (setting, "unset"), ("resharper_" + setting, "chop_always")));
            foreach (var value in new[] { null, "unset", "invalid", "wrap_if_long", "chop_if_long" })
            {
                var settings = value is null ? Array.Empty<(string, string)>() : new[] { (setting, value) };
                Assert.Equal(input, Format(input, settings));
            }
            Assert.Equal(input, Format(input, (setting, "invalid"), ("resharper_" + setting, "chop_always")));
            Assert.Equal(input, Format(input, (setting, "wrap_if_long"), ("resharper_" + setting, "chop_always")));
            Assert.Equal(input, Format(input, ("max_line_length", "10")));
        }

        [Theory]
        [InlineData("csharp_wrap_array_initializer_style", "var x = new[] { first, second, third, fourth };", "first,\n    second,\n    third,\n    fourth")]
        [InlineData("csharp_wrap_object_and_collection_initializer_style", "var x = new List<int> { first, second, third, fourth };", "first,\n    second,\n    third,\n    fourth")]
        [InlineData("csharp_wrap_list_pattern", "int[] x = [first, second, third, fourth];", "first,\n    second,\n    third,\n    fourth")]
        public void Width_and_existing_multiline_layout_trigger_chopping(string setting, string input, string elements)
        {
            Assert.Contains(elements, Format(input, (setting, "chop_if_long"), ("max_line_length", "30")));
            var multiline = input.Replace("second, ", "second,\n    ");
            Assert.Contains(elements, Format(multiline, (setting, "chop_if_long")));
            Assert.Equal(input, Format(input, (setting, "chop_if_long"), ("max_line_length", input.Length.ToString())));
            var greedy = Format(input, (setting, "wrap_if_long"), ("max_line_length", "30"));
            Assert.Contains("\n", greedy);
            Assert.Contains("third, fourth", greedy);
            Assert.All(greedy.Split('\n'), line => Assert.True(line.Length <= 30, line));
        }

        private static string Format(string input, params (string Key, string Value)[] settings)
        {
            var result = CSharpFormatterTests.Format(input, settings);
            Assert.Equal(result, CSharpFormatterTests.Format(result, settings));
            var original = CSharpSyntaxTree.ParseText(input).GetRoot();
            var formatted = CSharpSyntaxTree.ParseText(result);
            Assert.DoesNotContain(formatted.GetDiagnostics(), item => item.Severity == DiagnosticSeverity.Error);
            Assert.Equal(original.DescendantNodesAndTokens().Select(item => item.RawKind),
                formatted.GetRoot().DescendantNodesAndTokens().Select(item => item.RawKind));
            Assert.Equal(original.DescendantTokens().Select(token => token.Text),
                formatted.GetRoot().DescendantTokens().Select(token => token.Text));
            return result;
        }
    }
}
