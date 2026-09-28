// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace PNFmt.Tests.Formatter.CSharp
{
    public sealed class CSharpPropertyPatternWrappingTests
    {
        [Theory]
        [InlineData("csharp_wrap_property_pattern")]
        [InlineData("resharper_csharp_wrap_property_pattern")]
        public void Chop_always_accepts_both_spellings_without_a_width(string setting)
        {
            Assert.Equal("var x = value is\n{\n    A: 1,\n    B: 2\n};",
                Format("var x = value is { A: 1, B: 2 };", (setting, "CHOP_ALWAYS")));
        }

        [Fact]
        public void Chop_if_long_completes_multiline_patterns_without_a_width()
        {
            Assert.Equal("var x = value is\n{\n    A: 1,\n    B: 2\n};",
                Format("var x = value is { A: 1,\n    B: 2 };", ("csharp_wrap_property_pattern", "chop_if_long")));
        }

        [Theory]
        [InlineData("wrap_if_long")]
        [InlineData("chop_if_long")]
        [InlineData("chop_always")]
        public void Comments_directives_literals_and_exclusions_protect_their_boundaries(string style)
        {
            const string Protected = "// pnfmt: off\nvar off=value is{A:1,B:2};\n// pnfmt: on\n";
            const string Inactive = "#if NEVER\nvar inactive=value is{A:1,B:2};\n#endif\n";
            var result = Format(Protected + Inactive
                + "var x = value is { A: 1, /* attached */ B: 2, C: 3 };\n"
                + "var y = value is { A: 1, // attached\nB: 2 };\n"
                + "var z = value is { A: 1,\n#if DEBUG\nB: 2,\n#endif\nC: 3 };\n"
                + "var text = value is { Name: \"a long indivisible literal\" };\n"
                + "var interpolation = $\"{value is { A: 1, B: 2 }}\";",
                ("csharp_wrap_property_pattern", style), ("max_line_length", "20"));
            Assert.StartsWith(Protected + Inactive, result);
            Assert.Contains("A: 1, /* attached */ B: 2,", result);
            Assert.Contains("A: 1, // attached\n", result);
            Assert.Contains("#if DEBUG\nB: 2,\n#endif\n", result);
            Assert.Contains("\"a long indivisible literal\"", result);
            Assert.Contains("$\"{value is { A: 1, B: 2 }}\"", result);
        }

        [Fact]
        public void Csharp_name_takes_precedence_and_unset_allows_alias_fallback()
        {
            const string Input = "var x = value is { A: 1, B: 2 };";
            foreach (var value in new[] { "invalid", "wrap_if_long" })
            {
                Assert.Equal(Input, Format(Input, ("csharp_wrap_property_pattern", value),
                    ("resharper_csharp_wrap_property_pattern", "chop_always")));
            }
            Assert.Equal("var x = value is\n{\n    A: 1,\n    B: 2\n};", Format(Input,
                ("csharp_wrap_property_pattern", "unset"), ("resharper_csharp_wrap_property_pattern", "chop_always")));
        }

        [Theory]
        [InlineData("pnfmt_format")]
        [InlineData("pnfmt_csharp_format")]
        public void Disabled_layout_preserves_pattern_source(string setting)
        {
            const string Input = "var x=value is{A:1,B:2};";
            Assert.Equal(Input, Format(Input, (setting, "false"), ("csharp_wrap_property_pattern", "chop_always")));
        }

        [Fact]
        public void Empty_patterns_stay_compact_and_other_pattern_kinds_are_independent()
        {
            const string Input = "var x = value is { };\nvar y = value is [1, 2];\nvar z = value is (1, 2) or (3, 4);";
            Assert.Equal(Input, Format(Input, ("csharp_wrap_property_pattern", "chop_always")));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("unset")]
        [InlineData("invalid")]
        public void Missing_or_invalid_style_does_not_enable_property_pattern_wrapping(string style)
        {
            const string Input = "var x = value is { A: 1, B: 2 };";
            var settings = new[] { ("max_line_length", "10"), ("csharp_wrap_list_pattern", "chop_always") };
            Assert.Equal(Input, Format(Input, style is null ? settings
                : settings.Append(("csharp_wrap_property_pattern", style)).ToArray()));
        }

        [Fact]
        public void Nested_extended_and_positional_patterns_keep_their_syntax()
        {
            var result = Format("var x = value is Customer(1, 2) { Address.City.Length: > 0, Account: { Active: true, Level: 2 }, } customer;\n"
                + "var y = value is not { Name: null } and { Age: >= 18 and < 100 };",
                ("csharp_wrap_property_pattern", "chop_always"));
            Assert.Contains("Customer(1, 2)", result);
            Assert.Contains("Address.City.Length: > 0,", result);
            Assert.Contains("Account:\n    {\n        Active: true,\n        Level: 2\n    },", result);
            Assert.Contains("} customer;", result);
            Assert.Contains("Age: >= 18 and < 100", result);
        }

        [Fact]
        public void Roslyn_brace_placement_still_controls_pattern_opening_braces()
        {
            Assert.Equal("var x = value is {\n    A: 1,\n    B: 2\n};",
                Format("var x = value is { A: 1, B: 2 };", ("csharp_wrap_property_pattern", "chop_always"),
                    ("csharp_new_line_before_open_brace", "none")));
        }

        [Fact]
        public void Single_properties_trailing_commas_tabs_and_newlines_are_preserved()
        {
            Assert.Equal("var x = value is\r\n{\r\n\tA: 1,\r\n};", Format("var x = value is { A: 1, };",
                ("csharp_wrap_property_pattern", "chop_always"), ("indent_style", "tab"),
                ("indent_size", "tab"), ("tab_width", "4"), ("end_of_line", "crlf")));
        }

        [Theory]
        [InlineData("wrap_if_long")]
        [InlineData("chop_if_long")]
        public void Width_dependent_styles_preserve_short_patterns_and_exact_width(string style)
        {
            const string Input = "var x = value is { A: 1, B: 2 };";
            Assert.Equal(Input, Format(Input, ("csharp_wrap_property_pattern", style)));
            Assert.Equal(Input, Format(Input, ("csharp_wrap_property_pattern", style), ("max_line_length", Input.Length.ToString())));
            Assert.Equal(Input + " // a long trailing comment does not trigger wrapping",
                Format(Input + " // a long trailing comment does not trigger wrapping",
                    ("csharp_wrap_property_pattern", style), ("max_line_length", Input.Length.ToString())));
        }

        [Fact]
        public void Wrap_if_long_retains_properties_that_fit_together()
        {
            const string Input = "var x = value is { A: 1, B: 2, C: 3, D: 4 };";
            Assert.Equal("var x = value is\n{\n    A: 1,\n    B: 2, C: 3, D: 4\n};",
                Format(Input, ("max_line_length", "24"), ("csharp_wrap_property_pattern", "wrap_if_long")));
            Assert.Equal("var x = value is\n{\n    A: 1,\n    B: 2,\n    C: 3,\n    D: 4\n};",
                Format(Input, ("max_line_length", "24"), ("csharp_wrap_property_pattern", "chop_if_long")));
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
