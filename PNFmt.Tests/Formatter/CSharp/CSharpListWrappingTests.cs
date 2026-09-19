// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace PNFmt.Tests.Formatter.CSharp
{
    public sealed class CSharpListWrappingTests
    {
        [Theory]
        [InlineData("var x = new C(a, b);", "new C(\n")]
        [InlineData("C x = new(a, b);", "new(\n")]
        [InlineData("var x = values[a, b];", "values[\n")]
        [InlineData("Execute(name: a, other: b);", "Execute(\n")]
        public void Argument_style_applies_to_all_supported_lists(string input, string expected)
        {
            Assert.Contains(expected, Format(input, ("csharp_wrap_arguments_style", "chop_always")));
        }

        [Theory]
        [InlineData("wrap_if_long")]
        [InlineData("chop_if_long")]
        [InlineData("chop_always")]
        public void Arguments_and_parameters_can_use_different_styles(string style)
        {
            const string Input = "void M(int a, int b)\n{\nExecute(a, b);\n}";
            var parametersOnly = Format(Input, ("csharp_wrap_parameters_style", "chop_always"),
                ("csharp_wrap_arguments_style", style), ("max_line_length", "120"));
            Assert.Contains("void M(\n    int a,\n    int b)", parametersOnly);
            Assert.Contains(style == "chop_always" ? "Execute(\n" : "Execute(a, b)", parametersOnly);

            var argumentsOnly = Format(Input, ("csharp_wrap_parameters_style", style),
                ("csharp_wrap_arguments_style", "chop_always"), ("max_line_length", "120"));
            Assert.Contains("Execute(\n", argumentsOnly);
            Assert.Contains(style == "chop_always" ? "void M(\n" : "void M(int a, int b)", argumentsOnly);
        }

        [Theory]
        [InlineData("csharp_wrap_arguments_style", "Execute(a, b);", "Execute(\n    a,\n    b);")]
        [InlineData("resharper_csharp_wrap_arguments_style", "Execute(a, b);", "Execute(\n    a,\n    b);")]
        [InlineData("csharp_wrap_parameters_style", "delegate void D(int a, int b);", "delegate void D(\n    int a,\n    int b);")]
        [InlineData("resharper_csharp_wrap_parameters_style", "delegate void D(int a, int b);", "delegate void D(\n    int a,\n    int b);")]
        public void Chop_always_works_without_a_width_and_accepts_both_spellings(string setting, string input, string expected)
        {
            Assert.Equal(expected, Format(input, (setting, "chop_always")));
            Assert.Equal(expected, Format(input, (setting, "CHOP_ALWAYS"), ("max_line_length", "120")));
        }

        [Theory]
        [InlineData("csharp_wrap_arguments_style", "Execute(first,\n    second, third);", "Execute(\n    first,\n    second,\n    third);")]
        [InlineData("csharp_wrap_parameters_style", "delegate void D(int a,\n    int b, int c);", "delegate void D(\n    int a,\n    int b,\n    int c);")]
        public void Chop_if_long_also_chops_existing_multiline_lists_without_a_width(string setting, string input, string expected)
        {
            Assert.Equal(expected, Format(input, (setting, "chop_if_long")));
            Assert.Equal(expected, Format(input, (setting, "chop_if_long"), ("max_line_length", "120")));
        }

        [Fact]
        public void Chop_if_long_does_not_chop_a_list_exactly_at_width()
        {
            const string Input = "Execute(a, b);";
            Assert.Equal(Input, Format(Input, ("csharp_wrap_arguments_style", "chop_if_long"),
                ("max_line_length", Input.Length.ToString())));
        }

        [Theory]
        [InlineData("csharp_wrap_arguments_style", "Execute(first, second, third, fourth);", "Execute(\n    first,\n    second,\n    third,\n    fourth);")]
        [InlineData("csharp_wrap_parameters_style", "delegate void D(int a, int b, int c, int d);", "delegate void D(\n    int a,\n    int b,\n    int c,\n    int d);")]
        public void Chop_if_long_puts_each_item_on_a_line_when_width_is_exceeded(string setting, string input, string expected)
        {
            Assert.Equal(expected, Format(input, (setting, "chop_if_long"), ("max_line_length", "24")));
        }

        [Theory]
        [InlineData("wrap_if_long")]
        [InlineData("chop_if_long")]
        [InlineData("chop_always")]
        public void Comments_directives_interpolations_and_exclusions_remain_protected(string style)
        {
            const string Protected = "// pnfmt: off\nExecute( a,b,c );\n// pnfmt: on\n";
            const string Inactive = "#if NEVER\nExecute( a,b,c );\n#endif\n";
            var result = Format(Protected + Inactive
                + "Execute(first, /* keep */ second, third, fourth);\n"
                + "Execute(first, // attached\n    second, third);\n"
                + "var text = $\"{Execute(first, second, third)}\";\n",
                ("csharp_wrap_arguments_style", style), ("max_line_length", "24"));
            Assert.StartsWith(Protected + Inactive, result);
            Assert.Contains("first, /* keep */ second,", result);
            Assert.Contains("first, // attached\n", result);
            Assert.Contains("$\"{Execute(first, second, third)}\"", result);
        }

        [Theory]
        [InlineData("csharp_wrap_arguments_style", "Execute(a, b);")]
        [InlineData("csharp_wrap_parameters_style", "delegate void D(int a, int b);")]
        public void Csharp_names_take_precedence_over_vendor_aliases(string setting, string input)
        {
            Assert.Equal(input, Format(input, (setting, "wrap_if_long"), ("resharper_" + setting, "chop_always")));
            Assert.Equal(input, Format(input, (setting, "invalid"), ("resharper_" + setting, "chop_always")));
            Assert.Contains("\n", Format(input, (setting, "unset"), ("resharper_" + setting, "chop_always")));
        }

        [Fact]
        public void Editorconfig_inheritance_unset_and_file_preview_are_supported()
        {
            const string Input = "Execute(a, b);";
            using (var directory = new TestDirectory())
            {
                directory.Write(".editorconfig", "root = true\n[*.cs]\npnfmt_enabled = true\npnfmt_formatter = csharp\ncsharp_wrap_arguments_style = chop_always\n");
                var path = directory.Write("Child/Source.cs", Input);
                var formatter = new CSharpFormatter();
                Assert.Equal(FileFormatStatus.Updated, formatter.Format(new FileFormatRequest(path, false, false, NullFormatterLog.Instance)).Status);
                Assert.Equal(Input, File.ReadAllText(path));
                Assert.Equal(FileFormatStatus.Updated, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
                Assert.Equal("Execute(\n    a,\n    b);", File.ReadAllText(path));
                Assert.Equal(FileFormatStatus.Unchanged, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
                directory.Write("Child/.editorconfig", "[*.cs]\ncsharp_wrap_arguments_style = unset\n");
                File.WriteAllText(path, Input);
                Assert.Equal(FileFormatStatus.Unchanged, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
            }
        }

        [Theory]
        [InlineData("Execute();", "Execute();")]
        [InlineData("Execute(a);", "Execute(\n    a);")]
        [InlineData("delegate void D();", "delegate void D();")]
        [InlineData("delegate void D(int a);", "delegate void D(\n    int a);")]
        public void Empty_and_single_item_lists_have_stable_layouts(string input, string expected)
        {
            Assert.Equal(expected, Format(input, ("csharp_wrap_arguments_style", "chop_always"),
                ("csharp_wrap_parameters_style", "chop_always")));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("unset")]
        [InlineData("invalid")]
        public void Invalid_width_does_not_disable_explicit_chopping(string width)
        {
            Assert.Equal("Execute(\n    a,\n    b);", Format("Execute(a, b);",
                ("csharp_wrap_arguments_style", "chop_always"), ("max_line_length", width)));
            Assert.Equal("Execute(\n    a,\n    b);", Format("Execute(a,\n    b);",
                ("csharp_wrap_arguments_style", "chop_if_long"), ("max_line_length", width)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("unset")]
        [InlineData("UNSET")]
        [InlineData("unknown")]
        [InlineData("0")]
        public void Missing_or_invalid_styles_keep_the_original_width_only_behavior(string style)
        {
            var options = style is null ? Array.Empty<(string, string)>()
                : new[] { ("csharp_wrap_arguments_style", style), ("csharp_wrap_parameters_style", style) };
            const string Multiline = "Execute(a,\n    b, c);";
            Assert.Equal(Multiline, Format(Multiline, options));
            const string Long = "Execute(first, second, third, fourth);";
            Assert.Equal(Long, Format(Long, options));
            Assert.Equal("Execute(\n    first,\n    second,\n    third,\n    fourth);",
                Format(Long, options.Concat(new[] { ("max_line_length", "24") }).ToArray()));
            const string Parameters = "delegate void D(int a,\n    int b, int c);";
            Assert.Equal(Parameters, Format(Parameters, options));
        }

        [Fact]
        public void Newlines_and_tab_width_apply_to_greedy_wrapping()
        {
            const string Input = "void M()\n{\nExecute(first, second, third, fourth);\n}";
            var result = Format(Input, ("csharp_wrap_arguments_style", "wrap_if_long"), ("max_line_length", "31"),
                ("indent_style", "tab"), ("indent_size", "8"), ("tab_width", "8"), ("end_of_line", "crlf"));
            Assert.Contains("\tExecute(first, second,\r\n\t\tthird, fourth);", result);
        }

        [Theory]
        [InlineData("record C(int a, int b);", "record C(\n")]
        [InlineData("class C\n{\nC(int a, int b) { }\n}", "C(\n")]
        [InlineData("var f = (int a, int b) => a + b;", "(\n")]
        [InlineData("class C\n{\nint this[int a, int b] => 0;\n}", "this[\n")]
        public void Parameter_style_applies_to_all_supported_declarations(string input, string expected)
        {
            Assert.Contains(expected, Format(input, ("csharp_wrap_parameters_style", "chop_always")));
        }

        [Theory]
        [InlineData("wrap_if_long")]
        [InlineData("chop_if_long")]
        [InlineData("chop_always")]
        public void Styles_respect_disabled_layout_and_do_not_enable_unrelated_wrapping(string style)
        {
            const string Input = "Execute( a,b );";
            Assert.Equal(Input, Format(Input, ("csharp_wrap_arguments_style", style), ("max_line_length", "5"), ("pnfmt_format", "false")));
            Assert.Equal(Input, Format(Input, ("csharp_wrap_arguments_style", style), ("max_line_length", "5"), ("pnfmt_csharp_format", "false")));
            const string Other = "var result = firstCondition && secondCondition && thirdCondition;";
            Assert.Equal(Other, Format(Other, ("csharp_wrap_arguments_style", style), ("csharp_wrap_parameters_style", style)));
        }

        [Theory]
        [InlineData("wrap_if_long")]
        [InlineData("chop_if_long")]
        [InlineData("chop_always")]
        public void Unbreakable_items_still_terminate_and_preserve_their_contents(string style)
        {
            var result = Format("Execute(\"a string literal far longer than the target width\", secondArgument);",
                ("csharp_wrap_arguments_style", style), ("max_line_length", "8"));
            Assert.Contains("\"a string literal far longer than the target width\"", result);
            Assert.Contains("\n", result);
        }

        [Theory]
        [InlineData("chop_if_long")]
        [InlineData("wrap_if_long")]
        public void Width_dependent_styles_preserve_short_single_line_lists(string style)
        {
            const string Input = "delegate void D(int a, int b);";
            Assert.Equal(Input, Format(Input, ("csharp_wrap_parameters_style", style)));
            Assert.Equal(Input, Format(Input, ("csharp_wrap_parameters_style", style), ("max_line_length", "120")));
            Assert.Equal("Execute(a, b);", Format("Execute(a, b);", ("csharp_wrap_arguments_style", style)));
        }

        [Fact]
        public void Wrap_if_long_fills_multiple_continuation_lines_and_counts_closing_delimiters()
        {
            Assert.Equal("Execute(first, second,\n    third, fourth, fifth,\n    sixth, seventh);",
                Format("Execute(first, second, third, fourth, fifth, sixth, seventh);",
                    ("csharp_wrap_arguments_style", "wrap_if_long"), ("max_line_length", "25")));
            Assert.Equal("Execute(first,\n    second);", Format("Execute(first, second);",
                ("csharp_wrap_arguments_style", "wrap_if_long"), ("max_line_length", "21")));
        }

        [Theory]
        [InlineData("csharp_wrap_arguments_style", "Execute(first, second, third, fourth);", "24", "Execute(first, second,\n    third, fourth);")]
        [InlineData("csharp_wrap_parameters_style", "delegate void D(int a, int b, int c, int d);", "34", "delegate void D(int a, int b,\n    int c, int d);")]
        public void Wrap_if_long_keeps_multiple_items_on_lines_that_fit(string setting, string input, string width, string expected)
        {
            Assert.Equal(expected, Format(input, (setting, "wrap_if_long"), ("max_line_length", width)));
        }

        [Fact]
        public void Wrap_if_long_preserves_existing_breaks_and_short_nested_calls()
        {
            const string Multiline = "Execute(first,\n    second, third);";
            Assert.Equal(Multiline, Format(Multiline, ("csharp_wrap_arguments_style", "wrap_if_long"), ("max_line_length", "120")));
            Assert.Equal("Execute(First(a, b),\n    Second(c, d));", Format("Execute(First(a, b), Second(c, d));",
                ("csharp_wrap_arguments_style", "wrap_if_long"), ("max_line_length", "24")));
        }

        private static string Format(string input, params (string Key, string Value)[] options)
        {
            var result = CSharpFormatterTests.Format(input, options);
            Assert.Equal(result, CSharpFormatterTests.Format(result, options));
            var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp14);
            var originalRoot = CSharpSyntaxTree.ParseText(input, parseOptions).GetRoot();
            var resultTree = CSharpSyntaxTree.ParseText(result, parseOptions);
            Assert.DoesNotContain(resultTree.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
            Assert.Equal(originalRoot.DescendantNodesAndTokens().Select(item => item.RawKind),
                resultTree.GetRoot().DescendantNodesAndTokens().Select(item => item.RawKind));
            Assert.Equal(originalRoot.DescendantTokens().Select(token => token.Text),
                resultTree.GetRoot().DescendantTokens().Select(token => token.Text));
            return result;
        }
    }
}
