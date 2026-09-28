// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace PNFmt.Tests.Formatter.CSharp
{
    public sealed class CSharpConditionalWrappingTests
    {
        [Theory]
        [InlineData("csharp_wrap_ternary_expr_style")]
        [InlineData("resharper_csharp_wrap_ternary_expr_style")]
        public void Chop_always_accepts_both_spellings_without_a_width(string setting)
        {
            Assert.Equal("var x = ready\n    ? first\n    : second;",
                Format("var x = ready ? first : second;", (setting, "CHOP_ALWAYS")));
        }

        [Fact]
        public void Chop_if_long_completes_multiline_expressions_without_a_width()
        {
            Assert.Equal("var x = ready\n    ? first\n    : second;", Format("var x = ready\n    ? first : second;",
                ("csharp_wrap_ternary_expr_style", "chop_if_long")));
        }

        [Theory]
        [InlineData("wrap_if_long")]
        [InlineData("chop_if_long")]
        [InlineData("chop_always")]
        public void Comments_directives_literals_and_exclusions_retain_their_contents(string style)
        {
            const string Protected = "// pnfmt: off\nvar off=ready?first:second;\n// pnfmt: on\n";
            const string Inactive = "#if NEVER\nvar inactive=ready?first:second;\n#endif\n";
            var result = Format(Protected + Inactive
                + "var x = ready /* attached */ ? first : second;\n"
                + "var y = ready ? first // attached\n: second;\n"
                + "var z = ready ?\n#if DEBUG\nfirst\n#else\nsecond\n#endif\n: third;\n"
                + "var text = ready ? \"a very long indivisible literal\" : $\"{(ready ? first : second)}\";",
                ("csharp_wrap_ternary_expr_style", style), ("max_line_length", "20"));
            Assert.StartsWith(Protected + Inactive, result);
            Assert.Contains("ready /* attached */ ?", result);
            Assert.Contains("first // attached\n", result);
            Assert.Contains("#if DEBUG\nfirst\n#else\n", result);
            Assert.Contains("\"a very long indivisible literal\"", result);
            Assert.Contains("$\"{(ready ? first : second)}\"", result);
        }

        [Fact]
        public void Csharp_names_take_precedence_and_unset_allows_alias_fallback()
        {
            const string Input = "var x = ready ? first : second;";
            foreach (var value in new[] { "invalid", "wrap_if_long" })
            {
                Assert.Equal(Input, Format(Input, ("csharp_wrap_ternary_expr_style", value),
                    ("resharper_csharp_wrap_ternary_expr_style", "chop_always")));
            }
            Assert.Equal("var x = ready\n    ? first\n    : second;", Format(Input,
                ("csharp_wrap_ternary_expr_style", "unset"), ("resharper_csharp_wrap_ternary_expr_style", "chop_always"),
                ("csharp_wrap_before_ternary_opsigns", "invalid"), ("resharper_csharp_wrap_before_ternary_opsigns", "false")));
            Assert.Equal("var x = ready ?\n    first :\n    second;", Format(Input,
                ("csharp_wrap_ternary_expr_style", "chop_always"), ("csharp_wrap_before_ternary_opsigns", "unset"),
                ("resharper_csharp_wrap_before_ternary_opsigns", "false")));
        }

        [Theory]
        [InlineData("pnfmt_format")]
        [InlineData("pnfmt_csharp_format")]
        public void Disabled_layout_preserves_conditional_source(string setting)
        {
            const string Input = "var x=ready?first:second;";
            Assert.Equal(Input, Format(Input, (setting, "false"), ("csharp_wrap_ternary_expr_style", "chop_always")));
        }

        [Fact]
        public void Long_nested_branches_wrap_after_their_outer_expression()
        {
            const string Input = "var x = outer ? inner ? first : second : third;";
            Assert.Equal("var x = outer\n    ? inner\n        ? first\n        : second\n    : third;",
                Format(Input, ("csharp_wrap_ternary_expr_style", "chop_always")));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("unset")]
        [InlineData("invalid")]
        public void Missing_or_invalid_style_does_not_enable_ternary_wrapping(string style)
        {
            const string Input = "var x = ready ? first : second;";
            var settings = new[] { ("max_line_length", "10"), ("csharp_wrap_before_ternary_opsigns", "true") };
            Assert.Equal(Input, Format(Input, style is null ? settings
                : settings.Append(("csharp_wrap_ternary_expr_style", style)).ToArray()));
        }

        [Theory]
        [InlineData("true", "var x = ready\n    ? first\n    : second;")]
        [InlineData("false", "var x = ready ?\n    first :\n    second;")]
        [InlineData("FALSE", "var x = ready ?\n    first :\n    second;")]
        [InlineData("invalid", "var x = ready\n    ? first\n    : second;")]
        public void Operator_placement_is_independent_of_binary_operator_placement(string placement, string expected)
        {
            foreach (var setting in new[] { "csharp_wrap_before_ternary_opsigns", "resharper_csharp_wrap_before_ternary_opsigns" })
            {
                Assert.Equal(expected, Format("var x = ready ? first : second;",
                    ("csharp_wrap_ternary_expr_style", "chop_always"), (setting, placement),
                    ("dotnet_style_operator_placement_when_wrapping", "end_of_line")));
            }
        }

        [Fact]
        public void Ref_branches_and_nullable_operators_preserve_their_syntax()
        {
            var result = Format("ref int x = ref (ready ? ref first : ref second);\n"
                + "int? y = customer?.Active == true ? customer?.Value : fallback ?? defaultValue;",
                ("csharp_wrap_ternary_expr_style", "chop_always"));
            Assert.Contains("? ref first", result);
            Assert.Contains(": ref second", result);
            Assert.Contains("customer?.Active", result);
            Assert.Contains("customer?.Value", result);
            Assert.Contains("fallback ?? defaultValue", result);
        }

        [Fact]
        public void Tabs_and_configured_newlines_apply_to_ternary_continuations()
        {
            Assert.Equal("var x = ready\r\n\t? first\r\n\t: second;", Format("var x = ready ? first : second;",
                ("csharp_wrap_ternary_expr_style", "chop_always"), ("indent_style", "tab"),
                ("indent_size", "tab"), ("tab_width", "4"), ("end_of_line", "crlf")));
        }

        [Theory]
        [InlineData("wrap_if_long")]
        [InlineData("chop_if_long")]
        public void Width_dependent_styles_preserve_short_expressions_and_exact_width(string style)
        {
            const string Input = "var x = ready ? first : second;";
            Assert.Equal(Input, Format(Input, ("csharp_wrap_ternary_expr_style", style)));
            Assert.Equal(Input, Format(Input, ("csharp_wrap_ternary_expr_style", style), ("max_line_length", Input.Length.ToString())));
            Assert.Equal(Input + " // a very long trailing comment must not trigger wrapping",
                Format(Input + " // a very long trailing comment must not trigger wrapping",
                    ("csharp_wrap_ternary_expr_style", style), ("max_line_length", Input.Length.ToString())));
        }

        [Theory]
        [InlineData("true", "var x = ready ? first\n    : second;")]
        [InlineData("false", "var x = ready ? first :\n    second;")]
        public void Wrap_if_long_retains_parts_that_fit_together(string before, string expected)
        {
            Assert.Equal(expected, Format("var x = ready ? first : second;", ("max_line_length", "24"),
                ("csharp_wrap_ternary_expr_style", "wrap_if_long"), ("csharp_wrap_before_ternary_opsigns", before)));
            Assert.Equal("var x = ready\n    ? first\n    : second;", Format("var x = ready ? first : second;",
                ("max_line_length", "24"), ("csharp_wrap_ternary_expr_style", "chop_if_long")));
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
