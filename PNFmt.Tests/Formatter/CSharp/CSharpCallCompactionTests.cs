// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System.Linq;

using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace PNFmt.Tests.Formatter.CSharp
{
    public sealed class CSharpCallCompactionTests
    {
        [Fact]
        public void A_rejected_call_does_not_prevent_a_call_on_its_closing_line_from_compacting()
        {
            var result = Format("LongCallName(\nfirst,\nsecond); B(\nx,\ny);",
                ("csharp_keep_existing_linebreaks", "false"), ("max_line_length", "23"));
            Assert.Equal("LongCallName(\nfirst,\nsecond); B(x, y);", result);
        }

        [Theory]
        [InlineData("csharp_keep_existing_linebreaks")]
        [InlineData("resharper_csharp_keep_existing_linebreaks")]
        public void Both_spellings_join_short_calls_with_configured_spacing(string setting)
        {
            Assert.Equal("Process(first, second);", Format("Process(\n    first,\n    second\n);",
                (setting, "FALSE"), ("max_line_length", "80")));
            Assert.Equal("Process ( first, second );", Format("Process\n(\nfirst, second\n);",
                (setting, "false"), ("max_line_length", "80"),
                ("csharp_space_between_method_call_name_and_opening_parenthesis", "true"),
                ("csharp_space_between_method_call_parameter_list_parentheses", "true")));
        }

        [Fact]
        public void Calls_sharing_a_line_are_checked_against_the_combined_width()
        {
            var result = Format("A(\nx,\ny); B(\nx,\ny);",
                ("csharp_keep_existing_linebreaks", "false"), ("max_line_length", "16"));
            Assert.Equal("A(x, y); B(\nx,\ny);", result);
        }

        [Fact]
        public void Chopping_always_wins_but_short_multiline_calls_can_join_before_chop_if_long()
        {
            const string Input = "Process(\n    first,\n    second);";
            Assert.Equal(Input, Format(Input, ("csharp_keep_existing_linebreaks", "false"),
                ("csharp_wrap_arguments_style", "chop_always"), ("max_line_length", "80")));
            Assert.Equal("Process(first, second);", Format(Input, ("csharp_keep_existing_linebreaks", "false"),
                ("csharp_wrap_arguments_style", "chop_if_long"), ("max_line_length", "80")));
        }

        [Fact]
        public void Comments_directives_exclusions_and_multiline_arguments_are_never_flattened()
        {
            const string Protected = "// pnfmt: off\nProcess(\na,\nb);\n// pnfmt: on\n#if NEVER\nProcess(\na,\nb);\n#endif\n";
            var result = Format(Protected
                + "Process(\n    a, // attached\n    b);\n"
                + "Process(\n    a, /* keep */ b);\n"
                + "Process(\n#if NEVER\nx,\n#endif\ny);\n"
                + "Process(\n    a +\n    b);\n"
                + "Process(\n    \"\"\"\n    raw\n    text\n    \"\"\");\n"
                + "var s = $\"\"\"\n    {Process(\n        a,\n        b)}\n    \"\"\";",
                ("csharp_keep_existing_linebreaks", "false"), ("max_line_length", "120"));
            Assert.StartsWith(Protected, result);
            Assert.Contains("a, // attached\n", result);
            Assert.Contains("Process(\n    a, /* keep */ b);", result);
            Assert.Contains("#if NEVER\nx,\n#endif\n", result);
            Assert.Contains("a +\n", result);
            Assert.Contains("\"\"\"\n    raw\n    text\n    \"\"\"", result);
            Assert.Contains("{Process(\n        a,\n        b)}", result);
        }

        [Fact]
        public void Csharp_name_wins_and_unset_allows_alias_fallback()
        {
            const string Input = "Process(\n    first,\n    second\n);";
            Assert.Equal(Input, Format(Input, ("csharp_keep_existing_linebreaks", "invalid"),
                ("resharper_csharp_keep_existing_linebreaks", "false"), ("max_line_length", "80")));
            Assert.Equal("Process(first, second);", Format(Input, ("csharp_keep_existing_linebreaks", "unset"),
                ("resharper_csharp_keep_existing_linebreaks", "false"), ("max_line_length", "80")));
        }

        [Theory]
        [InlineData("pnfmt_format")]
        [InlineData("pnfmt_csharp_format")]
        public void Disabled_layout_preserves_source(string setting)
        {
            const string Input = "Process(\na,\nb\n);";
            Assert.Equal(Input, Format(Input, (setting, "false"), ("csharp_keep_existing_linebreaks", "false"), ("max_line_length", "80")));
        }

        [Fact]
        public void Entire_line_including_suffix_indentation_and_tab_stops_must_fit()
        {
            const string Input = "Process(\n    first,\n    second\n);";
            Assert.Equal("Process(first, second);", Format(Input, ("csharp_keep_existing_linebreaks", "false"), ("max_line_length", "23")));
            Assert.Equal(Input, Format(Input, ("csharp_keep_existing_linebreaks", "false"), ("max_line_length", "22")));
            Assert.Equal(Input + " // comment", Format(Input + " // comment",
                ("csharp_keep_existing_linebreaks", "false"), ("max_line_length", "22")));
            Assert.Equal("Process(first, second); // long comment is ignored", Format(Input + " // long comment is ignored",
                ("csharp_keep_existing_linebreaks", "false"), ("max_line_length", "23")));
            var tabbed = Format("void M()\n{\nProcess(\nfirst,\nsecond\n);\n}", ("csharp_keep_existing_linebreaks", "false"),
                ("max_line_length", "30"), ("indent_style", "tab"), ("indent_size", "8"), ("tab_width", "8"), ("end_of_line", "crlf"));
            Assert.Contains("\tProcess(\r\n", tabbed);
            Assert.DoesNotContain("\n", tabbed.Replace("\r\n", ""));
        }

        [Fact]
        public void Independent_calls_compact_while_a_call_that_exceeds_the_width_keeps_its_breaks()
        {
            var result = Format("Short(\na,\nb);\nThisCallNameDoesNotFit(\na,\nb);\nOther(\nx,\ny);",
                ("csharp_keep_existing_linebreaks", "false"), ("max_line_length", "20"));
            Assert.Equal("Short(a, b);\nThisCallNameDoesNotFit(\na,\nb);\nOther(x, y);", result);
        }

        [Fact]
        public void Joining_after_outer_wrapping_is_stable()
        {
            var result = Format("object M() => veryLongConditionName ? Process(\nfirst,\nsecond\n) : fallback;",
                ("csharp_keep_existing_linebreaks", "false"), ("csharp_wrap_ternary_expr_style", "chop_if_long"), ("max_line_length", "45"));
            Assert.Contains("? Process(first, second)", result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("invalid")]
        [InlineData("0")]
        [InlineData("-1")]
        public void Joining_requires_a_positive_width(string width)
        {
            const string Input = "Process(\n    first,\n    second\n);";
            var settings = new[] { ("csharp_keep_existing_linebreaks", "false") };
            Assert.Equal(Input, Format(Input, width is null ? settings : settings.Append(("max_line_length", width)).ToArray()));
        }

        [Fact]
        public void Layout_is_formatted_when_no_calls_need_joining()
        {
            var result = Format("class C\n{\nvoid M()\n{\nF();\n}\n}",
                ("csharp_keep_existing_linebreaks", "false"), ("max_line_length", "80"));
            Assert.Equal("class C\n{\n    void M()\n    {\n        F();\n    }\n}", result);
        }

        [Fact]
        public void Nested_calls_constructors_indexers_empty_and_single_arguments_join()
        {
            var result = Format("Outer(\nInner(\na,\nb\n),\nc\n);\nvar x = new C(\na,\nb\n);\nvar y = x[\na,\nb\n];\nEmpty(\n);\nOne(\na\n);",
                ("csharp_keep_existing_linebreaks", "false"), ("max_line_length", "80"));
            Assert.Equal("Outer(Inner(a, b), c);\nvar x = new C(a, b);\nvar y = x[a, b];\nEmpty();\nOne(a);", result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("true")]
        [InlineData("invalid")]
        [InlineData("unset")]
        public void Preservation_remains_the_default(string value)
        {
            const string Input = "Process(\n    first,\n    second\n);";
            var settings = new[] { ("max_line_length", "80") };
            Assert.Equal(Input, Format(Input, value is null ? settings : settings.Append(("csharp_keep_existing_linebreaks", value)).ToArray()));
        }

        private static string Format(string input, params (string Key, string Value)[] settings)
        {
            var result = CSharpFormatterTests.Format(input, settings);
            Assert.Equal(result, CSharpFormatterTests.Format(result, settings));
            var original = CSharpSyntaxTree.ParseText(input).GetRoot();
            var formatted = CSharpSyntaxTree.ParseText(result).GetRoot();
            Assert.Equal(original.DescendantNodesAndTokens().Select(item => item.RawKind),
                formatted.DescendantNodesAndTokens().Select(item => item.RawKind));
            Assert.Equal(original.DescendantTokens().Select(token => token.Text), formatted.DescendantTokens().Select(token => token.Text));
            return result;
        }
    }
}
