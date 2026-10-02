// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace PNFmt.Tests.Formatter.CSharp
{
    public sealed class CSharpRemainingWrappingTests
    {
        [Theory]
        [InlineData("csharp_wrap_linq_expressions", "var q = from x in xs where x.Active select x;", "var q = from x in xs\n        where x.Active\n        select x;")]
        [InlineData("csharp_wrap_switch_expression", "var x = n switch { 0 => a, _ => b };", "var x = n switch\n{\n    0 => a,\n    _ => b\n};")]
        [InlineData("pnfmt_csharp_wrap_tuple_style", "var x = (a, b);", "var x = (\n    a,\n    b);")]
        public void Chop_always_is_opt_in_and_works_without_width(string setting, string input, string expected)
        {
            Assert.Equal(expected, Format(input, (setting, "CHOP_ALWAYS")));
            foreach (var value in new[] { null, "invalid", "unset", "wrap_if_long", "chop_if_long" })
            {
                Assert.Equal(input, Format(input, value is null ? new (string, string)[0] : new[] { (setting, value) }));
            }
            Assert.Equal(input, Format(input, ("max_line_length", "10")));
        }

        [Theory]
        [InlineData("csharp_wrap_linq_expressions", "var q = from x in xs\n        where x.Active select x;", "where x.Active\n        select x;")]
        [InlineData("csharp_wrap_switch_expression", "var x = n switch {\n    0 => a, _ => b };", "0 => a,\n    _ => b")]
        [InlineData("pnfmt_csharp_wrap_tuple_style", "var x = (a,\n    b, c);", "a,\n    b,\n    c);")]
        public void Chop_if_long_completes_existing_multiline_constructs_without_width(string setting, string input, string expected)
        {
            Assert.Contains(expected, Format(input, (setting, "chop_if_long")));
        }

        [Theory]
        [InlineData("wrap_if_long")]
        [InlineData("chop_if_long")]
        [InlineData("chop_always")]
        public void Comments_directives_literals_and_exclusions_remain_protected(string style)
        {
            const string Protected = "// pnfmt: off\nvar off=(a,b);\n// pnfmt: on\n#if NEVER\nvar disabled=(a,b);\n#endif\n";
            var result = Format(Protected + "var t = (a, /* keep */ b, c);\n"
                + "var s = n switch { 0 => \"a long indivisible literal\", /* keep */ _ => b };\n"
                + "var q = from x in xs /* keep */ where x.Active select x;\n"
                + "var d = n switch { 0 => a,\n#if NEVER\n1=>x,\n#endif\n_ => b };\n"
                + "var text = $\"{(a, b)} {n switch { 0 => a, _ => b }}\";",
                ("csharp_wrap_linq_expressions", style), ("csharp_wrap_switch_expression", style),
                ("pnfmt_csharp_wrap_tuple_style", style), ("max_line_length", "15"));
            Assert.StartsWith(Protected, result);
            Assert.Contains("a, /* keep */ b", result);
            Assert.Contains("\"a long indivisible literal\"", result);
            Assert.Contains("#if NEVER\n1=>x,\n#endif\n", result);
            Assert.Contains("$\"{(a, b)} {n switch { 0 => a, _ => b }}\"", result);
        }

        [Theory]
        [InlineData("csharp_wrap_linq_expressions", "var q = from x in xs select x;")]
        [InlineData("csharp_wrap_switch_expression", "var x = n switch { _ => a };")]
        [InlineData("pnfmt_csharp_wrap_tuple_style", "var x = (a, b);")]
        public void Configured_physical_newlines_apply_to_all_styles(string setting, string input)
        {
            var result = Format(input, (setting, "chop_always"), ("end_of_line", "crlf"),
                ("indent_style", "tab"), ("indent_size", "tab"), ("tab_width", "4"),
                ("csharp_wrap_before_linq_expression", "true"));
            Assert.Contains("\r\n\t", result);
            Assert.DoesNotContain("\n", result.Replace("\r\n", ""));
        }

        [Theory]
        [InlineData("pnfmt_format")]
        [InlineData("pnfmt_csharp_format")]
        public void Disabled_layout_disables_all_new_wrap_styles(string setting)
        {
            const string Input = "var q=from x in xs select x;var y=n switch{0=>a,_=>b};var z=(a,b);";
            Assert.Equal(Input, Format(Input, (setting, "false"), ("csharp_wrap_linq_expressions", "chop_always"),
                ("csharp_wrap_switch_expression", "chop_always"), ("pnfmt_csharp_wrap_tuple_style", "chop_always")));
        }

        [Fact]
        public void First_query_clause_placement_applies_after_simple_wrapping_and_to_existing_multiline_queries()
        {
            Assert.StartsWith("var q =\n    from x in xs", Format("var q = from x in xs where x.Active select x;",
                ("csharp_wrap_linq_expressions", "wrap_if_long"), ("max_line_length", "32"),
                ("csharp_wrap_before_linq_expression", "true"), ("csharp_new_line_between_query_expression_clauses", "false")));
            Assert.Equal("var q =\n    from x in xs\n    select x;", Format("var q = from x in xs\n        select x;",
                ("csharp_wrap_linq_expressions", "wrap_if_long"), ("csharp_wrap_before_linq_expression", "true")));
        }

        [Fact]
        public void Queries_respect_first_clause_placement_and_preserve_nested_query_structure()
        {
            const string Input = "var q = from x in xs where x.Active select x;";
            foreach (var spelling in new[] { "csharp_wrap_before_linq_expression", "resharper_csharp_wrap_before_linq_expression" })
            {
                Assert.Equal("var q =\n    from x in xs\n    where x.Active\n    select x;",
                    Format(Input, ("csharp_wrap_linq_expressions", "chop_always"), (spelling, "TRUE")));
                Assert.Equal(Input, Format(Input, (spelling, "true")));
            }
            Assert.Equal("var q = from x in xs\n        where x.Active\n        select x;",
                Format(Input, ("csharp_wrap_linq_expressions", "chop_always"),
                    ("csharp_wrap_before_linq_expression", "invalid"), ("resharper_csharp_wrap_before_linq_expression", "true")));
            var result = Format("var q = from x in xs from y in x.Children let z = y.Value join a in ys on z equals a.Key into matches "
                + "orderby z, x.Name descending group matches by z into grouped where grouped.Any() select grouped;\n"
                + "var nested = from x in xs let inner = (from y in ys where y.Active select y) select inner;",
                ("csharp_wrap_linq_expressions", "chop_always"), ("csharp_wrap_before_linq_expression", "true"));
            Assert.Contains("join a in ys on z equals a.Key into matches", result);
            Assert.Contains("orderby z, x.Name descending", result);
            Assert.Contains("group matches by z\n", result);
            Assert.Contains("into grouped\n", result);
            Assert.Contains("from y in ys\n", result);
        }

        [Fact]
        public void Simple_wrap_can_keep_multiple_clauses_arms_and_tuple_elements_together()
        {
            Assert.Contains("where x.Active select x;", Format("var q = from x in xs where x.Active select x;",
                ("csharp_wrap_linq_expressions", "wrap_if_long"), ("max_line_length", "32"),
                ("csharp_new_line_between_query_expression_clauses", "false")));
            Assert.Contains("1 => b, _ => c", Format("var x = n switch { 0 => a, 1 => b, _ => c };",
                ("csharp_wrap_switch_expression", "wrap_if_long"), ("max_line_length", "26")));
            Assert.Equal("var x = (first, second,\n    third, fourth);", Format("var x = (first, second, third, fourth);",
                ("pnfmt_csharp_wrap_tuple_style", "wrap_if_long"), ("max_line_length", "24")));
        }

        [Fact]
        public void Switch_arms_preserve_guards_patterns_nested_switches_and_trailing_commas()
        {
            var result = Format("var x = value switch { { Active: true } c when c.Ready => c.Kind switch { 1 => a, _ => b, }, _ => fallback, };",
                ("csharp_wrap_switch_expression", "chop_always"));
            Assert.Contains("{ Active: true } c when c.Ready => c.Kind switch\n", result);
            Assert.Contains("1 => a,\n", result);
            Assert.Contains("_ => b,\n", result);
            Assert.Contains("_ => fallback,\n", result);
            Assert.Equal("var x = value switch { };", Format("var x = value switch { };", ("csharp_wrap_switch_expression", "chop_always")));
            Assert.Contains("value switch {\n", Format("var x = value switch { _ => fallback };",
                ("csharp_wrap_switch_expression", "chop_always"), ("csharp_new_line_before_open_brace", "none")));
        }

        [Fact]
        public void Tuple_types_expressions_and_nested_deconstruction_keep_names_and_types()
        {
            var result = Format("(int first, (string name, int age) person) value = (1, (\"A\", 2));\n"
                + "var (left, (middle, right)) = value;\n"
                + "(int a, int b) = pair;\nvar named = (Name: \"A\", Age: 2);",
                ("pnfmt_csharp_wrap_tuple_style", "chop_always"));
            Assert.Contains("int first,\n", result);
            Assert.Contains("string name,\n", result);
            Assert.Contains("int age) person)", result);
            Assert.Contains("middle,\n", result);
            Assert.Contains("Name: \"A\",\n", result);
        }

        [Theory]
        [InlineData("csharp_wrap_linq_expressions", "var q = from x in xs where x.Active select x;")]
        [InlineData("csharp_wrap_switch_expression", "var x = n switch { 0 => a, _ => b };")]
        public void Vendor_aliases_follow_csharp_precedence(string setting, string input)
        {
            var expected = Format(input, (setting, "chop_always"));
            Assert.Equal(expected, Format(input, ("resharper_" + setting, "chop_always")));
            Assert.Equal(expected, Format(input, (setting, "unset"), ("resharper_" + setting, "chop_always")));
            Assert.Equal(input, Format(input, (setting, "invalid"), ("resharper_" + setting, "chop_always")));
            Assert.Equal(input, Format(input, (setting, "wrap_if_long"), ("resharper_" + setting, "chop_always")));
        }

        [Theory]
        [InlineData("csharp_wrap_linq_expressions", "var q = from x in xs where x.Active select x;")]
        [InlineData("csharp_wrap_switch_expression", "var x = n switch { 0 => a, _ => b };")]
        [InlineData("pnfmt_csharp_wrap_tuple_style", "var x = (a, b);")]
        public void Width_styles_preserve_exact_width_and_ignore_trailing_comments(string setting, string input)
        {
            foreach (var style in new[] { "wrap_if_long", "chop_if_long" })
            {
                Assert.Equal(input, Format(input, (setting, style), ("max_line_length", input.Length.ToString())));
                var comment = input + " // this trailing comment is longer than the width";
                Assert.Equal(comment, Format(comment, (setting, style), ("max_line_length", input.Length.ToString())));
                Assert.Contains("\n", Format(input, (setting, style), ("max_line_length", "12")));
            }
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
            Assert.Equal(original.DescendantTrivia().Where(trivia => trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                    || trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)).Select(trivia => trivia.ToString()),
                formatted.GetRoot().DescendantTrivia().Where(trivia => trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                    || trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)).Select(trivia => trivia.ToString()));
            return result;
        }
    }
}
