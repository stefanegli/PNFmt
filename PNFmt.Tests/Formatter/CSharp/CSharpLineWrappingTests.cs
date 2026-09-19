// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Globalization;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace PNFmt.Tests.Formatter.CSharp
{
    public sealed class CSharpLineWrappingTests
    {
        [Theory]
        [InlineData("beginning_of_line", "var result = firstCondition\n    && secondCondition\n    && thirdCondition;")]
        [InlineData("end_of_line", "var result = firstCondition &&\n    secondCondition &&\n    thirdCondition;")]
        public void Binary_expressions_honor_operator_placement(string placement, string expected)
        {
            Assert.Equal(expected, Format("var result = firstCondition && secondCondition && thirdCondition;", 32,
                ("dotnet_style_operator_placement_when_wrapping", placement)));
        }

        [Fact]
        public void Binary_operators_default_to_beginning_of_line()
        {
            Assert.Equal("var result = firstCondition\n    && secondCondition;",
                Format("var result = firstCondition && secondCondition;", 30));
        }

        [Fact]
        public void Comments_remain_attached_and_do_not_trigger_wrapping_of_short_code()
        {
            const string Input = "Execute(1, 2); // a very long comment that should stay here without wrapping the call";
            Assert.Equal(Input, Format(Input, 30));
            var result = Format("Execute(firstArgument, /* keep with second */ secondArgument, thirdArgument);", 30);
            Assert.Contains("firstArgument, /* keep with second */ secondArgument,", result);
            Assert.Contains("\n    thirdArgument", result);
            Assert.Contains("firstArgument, // keep with first\n", Format(
                "Execute(firstArgument, // keep with first\nsecondArgument, thirdArgument);", 30));
        }

        [Fact]
        public void Custom_space_indentation_applies_to_continuations()
        {
            Assert.Equal("Execute(\n  firstArgument,\n  secondArgument);",
                Format("Execute(firstArgument, secondArgument);", 30, ("indent_size", "2")));
        }

        [Fact]
        public void Detects_existing_newlines()
        {
            Assert.Equal("Execute(\r\n    firstArgument,\r\n    secondArgument);\r\n",
                Format("Execute(firstArgument, secondArgument);\r\n", 30));
        }

        [Fact]
        public void Excluded_regions_remain_exact_while_surrounding_code_wraps()
        {
            const string Protected = "// pnfmt: off\nExecute(  firstArgument,secondArgument,thirdArgument  );\n// pnfmt: on\n";
            var result = Format(Protected + "Execute(firstArgument, secondArgument, thirdArgument);", 30);
            Assert.StartsWith(Protected, result);
            Assert.Contains("Execute(\n", result);
        }

        [Fact]
        public void Existing_line_breaks_are_retained()
        {
            const string Input = "Execute(\n    firstArgument,\n    secondArgument);";
            Assert.Equal(Input, Format(Input, 120));
        }

        [Fact]
        public void File_formatter_honors_inheritance_preview_and_unset()
        {
            const string Input = "Execute(firstArgument, secondArgument);";
            using (var directory = new TestDirectory())
            {
                directory.Write(".editorconfig", "root = true\n[*.cs]\npnfmt_enabled = true\npnfmt_formatter = csharp\nmax_line_length = 30\n");
                var path = directory.Write("Child/Source.cs", Input);
                var formatter = new CSharpFormatter();
                Assert.Equal(FileFormatStatus.Updated, formatter.Format(new FileFormatRequest(path, false, false, NullFormatterLog.Instance)).Status);
                Assert.Equal(Input, File.ReadAllText(path));
                Assert.Equal(FileFormatStatus.Updated, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
                Assert.Contains("Execute(\n", File.ReadAllText(path));
                Assert.Equal(FileFormatStatus.Unchanged, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);

                directory.Write("Child/.editorconfig", "[*.cs]\nmax_line_length = unset\n");
                File.WriteAllText(path, Input);
                Assert.Equal(FileFormatStatus.Unchanged, formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
                Assert.Equal(Input, File.ReadAllText(path));
            }
        }

        [Theory]
        [InlineData("lf", "\n")]
        [InlineData("crlf", "\r\n")]
        [InlineData("cr", "\r")]
        public void Honors_configured_newlines(string setting, string newline)
        {
            Assert.Equal("Execute(" + newline + "    firstArgument," + newline + "    secondArgument);",
                Format("Execute(firstArgument, secondArgument);", 30, ("end_of_line", setting)));
        }

        [Fact]
        public void Layout_switch_disables_wrapping()
        {
            const string Input = "Execute(firstArgument,secondArgument);";
            Assert.Equal(Input, Format(Input, 20, ("pnfmt_format", "false")));
            Assert.Equal(Input, Format(Input, 20, ("pnfmt_csharp_format", "false")));
        }

        [Fact]
        public void Measures_after_spacing_and_indentation()
        {
            var result = Format("class C\n{\nvoid M()\n{\nExecute(firstArgument,secondArgument);\n}\n}\n", 42);
            Assert.Contains("        Execute(\n            firstArgument,\n            secondArgument);", result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("unset")]
        [InlineData("UNSET")]
        [InlineData("")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("120.5")]
        [InlineData("invalid")]
        [InlineData("999999999999999999999")]
        public void Missing_or_invalid_width_disables_wrapping(string width)
        {
            const string Input = "Execute(firstArgument, secondArgument, thirdArgument);";
            var settings = width is null ? Array.Empty<(string, string)>() : new[] { ("max_line_length", width) };
            Assert.Equal(Input, CSharpFormatterTests.Format(Input, settings));
        }

        [Theory]
        [InlineData("class C { void M() { Execute(firstArgument, secondArgument, thirdArgument); } }")]
        [InlineData("if (firstCondition && secondCondition && thirdCondition) { Execute(firstArgument, secondArgument); }")]
        [InlineData("var result = source.Where(x => x.FirstCondition && x.SecondCondition).Select(selector).ToList();")]
        [InlineData("var result = source.Where(predicate)?.Select(selector)?.ToList();")]
        [InlineData("var result = source.First().Value.Second().Third();")]
        [InlineData("Execute(\n    First(one, two), Second(thirdArgument, fourthArgument));")]
        [InlineData("Execute(\"\"\"\n    first line\n    second line\n    \"\"\", firstArgument, secondArgument);")]
        public void Mixed_constructs_remain_valid_and_idempotent(string input)
        {
            Assert.Contains("\n", Format(input, 30));
        }

        [Theory]
        [InlineData("firstValue + secondValue * thirdValue - fourthValue")]
        [InlineData("firstValue ?? secondValue ?? thirdValue")]
        [InlineData("firstValue + (secondValue * thirdValue)")]
        [InlineData("firstValue is string && secondValue is string")]
        public void Preserves_binary_expression_structure(string expression)
        {
            var result = Format("var result = " + expression + ";", 28);
            Assert.Contains("\n", result);
        }

        [Fact]
        public void Strings_interpolations_directives_and_inactive_text_are_preserved()
        {
            const string Input = "#if NEVER\nExecute(  firstArgument,secondArgument,thirdArgument  );\n#endif\n"
                + "Execute(\"a very long string literal that cannot be split\", otherArgument);\n"
                + "var text = $\"{Execute(firstArgument, secondArgument)}\";\n"
                + "var raw = \"\"\"\n    a very long raw string line that cannot be split\n    \"\"\";\n";
            var result = Format(Input, 30);
            Assert.Contains("#if NEVER\nExecute(  firstArgument,secondArgument,thirdArgument  );\n#endif", result);
            Assert.Contains("\"a very long string literal that cannot be split\"", result);
            Assert.Contains("$\"{Execute(firstArgument, secondArgument)}\"", result);
        }

        [Fact]
        public void Tab_stops_count_toward_width_and_new_lines_use_configured_indentation()
        {
            const string Input = "class C\n{\nvoid M()\n{\nExecute(firstArgument, secondArgument);\n}\n}";
            var result = Format(Input, 48, ("indent_style", "tab"), ("indent_size", "8"), ("tab_width", "8"));
            Assert.Contains("\t\tExecute(\n\t\t\tfirstArgument,\n\t\t\tsecondArgument);", result);
        }

        [Fact]
        public void Width_is_a_target_when_no_safe_break_exists()
        {
            const string Input = "var identifierThatCannotBeSplitAtAll = 1234567890;";
            Assert.Equal(Input, Format(Input, 1));
            Assert.Contains("Execute(\n", Format("Execute(firstArgument, secondArgument);", 1));
        }

        [Fact]
        public void Width_is_inclusive()
        {
            const string Input = "Execute(firstArgument, secondArgument);";
            Assert.Equal(Input, Format(Input, Input.Length));
            Assert.Contains("Execute(\n", Format(Input, Input.Length - 1));
        }

        [Fact]
        public void Wrapping_respects_exclusions_after_sorting_and_region_removal()
        {
            const string Protected = "// pnfmt: off\r\nvoid Keep(){ Execute(  firstArgument,secondArgument  ); }  \r\n// pnfmt: on\r\n";
            var input = "#region Imports\nusing Z;\nusing A;\n#endregion\nclass C\n{\n" + Protected
                + "void Anchor() { }\nstatic public void Z() { }\nstatic public void A() { Execute(firstArgument, secondArgument); }\n}\n";
            var options = new[] { ("max_line_length", "40"), ("pnfmt_sort_entries", "true"),
                ("pnfmt_csharp_remove_regions", "true"), ("pnfmt_csharp_sort_members", "true"),
                ("pnfmt_csharp_sort_modifiers", "true"), ("pnfmt_csharp_collapse_blank_lines", "true"),
                ("trim_trailing_whitespace", "true"), ("end_of_line", "lf") };
            var result = CSharpFormatterTests.Format(input, options);
            Assert.Contains(Protected, result);
            Assert.StartsWith("using A;\nusing Z;\n", result);
            Assert.DoesNotContain("#region", result);
            Assert.Contains("Execute(\n", result);
            Assert.True(result.IndexOf("void A", StringComparison.Ordinal) < result.IndexOf("void Z", StringComparison.Ordinal));
            Assert.Equal(result, CSharpFormatterTests.Format(result, options));
        }

        [Fact]
        public void Wraps_arguments_at_one_item_per_line()
        {
            Assert.Equal("Execute(\n    firstArgument,\n    secondArgument,\n    thirdArgument);",
                Format("Execute(firstArgument, secondArgument, thirdArgument);", 30));
        }

        [Theory]
        [InlineData("source.Where(predicate).Select(selector).ToList()", "source\n    .Where(predicate)\n    .Select(selector)\n    .ToList()")]
        [InlineData("source?.Where(predicate).Select(selector).ToList()", "source\n    ?.Where(predicate)\n    .Select(selector)\n    .ToList()")]
        [InlineData("source.Where(predicate).Select(selector).Count", "source\n    .Where(predicate)\n    .Select(selector).Count")]
        [InlineData("GetSource().Where(predicate)", "GetSource()\n    .Where(predicate)")]
        [InlineData("GetSource()?.Where(predicate)", "GetSource()\n    ?.Where(predicate)")]
        public void Wraps_chained_calls(string expression, string expected)
        {
            Assert.Equal("var result = " + expected + ";", Format("var result = " + expression + ";", 32));
        }

        [Fact]
        public void Wraps_nested_calls_that_still_exceed_width()
        {
            var result = Format("Execute(First(firstArgument, secondArgument), Second(thirdArgument, fourthArgument));", 28);
            Assert.Contains("    First(\n        firstArgument,\n        secondArgument)", result);
            Assert.Contains("    Second(\n        thirdArgument,\n        fourthArgument)", result);
            Assert.All(result.Split('\n'), line => Assert.True(line.Length <= 28, line));
        }

        [Theory]
        [InlineData("var value = new Something(firstArgument, secondArgument);", "new Something(\n")]
        [InlineData("Something value = new(firstArgument, secondArgument);", "new(\n")]
        [InlineData("var value = values[firstArgument, secondArgument];", "values[\n")]
        [InlineData("Execute(name: firstArgument, other: secondArgument);", "Execute(\n")]
        [InlineData("Execute(ref firstArgument, out secondArgument);", "Execute(\n")]
        public void Wraps_other_argument_lists(string input, string expected)
        {
            Assert.Contains(expected, Format(input, 35));
        }

        [Fact]
        public void Wraps_outer_call_before_deciding_whether_nested_calls_need_wrapping()
        {
            Assert.Equal("Execute(\n    First(one, two),\n    Second(three, four));",
                Format("Execute(First(one, two), Second(three, four));", 25));
        }

        [Theory]
        [InlineData("class C\n{\nvoid Configure(string firstParameter, string secondParameter) { }\n}", "void Configure(\n")]
        [InlineData("record C(string firstParameter, string secondParameter);", "record C(\n")]
        [InlineData("class C\n{\nC(string firstParameter, string secondParameter) { }\n}", "C(\n")]
        [InlineData("delegate void Configure(string firstParameter, string secondParameter);", "Configure(\n")]
        [InlineData("void Configure(string firstParameter, string secondParameter) { }", "Configure(\n")]
        [InlineData("var f = (string firstParameter, string secondParameter) => firstParameter;", "(\n")]
        [InlineData("class C\n{\nint this[string firstParameter, string secondParameter] => 0;\n}", "this[\n")]
        public void Wraps_parameter_lists(string input, string expected)
        {
            Assert.Contains(expected, Format(input, 40));
        }

        private static string Format(string input, int width, params (string Key, string Value)[] settings)
        {
            var options = settings.Concat(new[] { ("max_line_length", width.ToString(CultureInfo.InvariantCulture)) }).ToArray();
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
