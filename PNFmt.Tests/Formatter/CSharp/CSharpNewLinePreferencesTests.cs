// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System.Linq;

using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace PNFmt.Tests.Formatter.CSharp
{
    public sealed class CSharpNewLinePreferencesTests
    {
        private const string Braces = "csharp_style_allow_blank_lines_between_consecutive_braces_experimental";
        private const string Multiple = "dotnet_style_allow_multiple_blank_lines_experimental";
        private const string Statements = "dotnet_style_allow_statement_immediately_after_block_experimental";

        [Fact]
        public void Adds_separation_when_whitespace_formatting_expands_single_line_statements()
        {
            Assert.Equal("if (true) { }\n\nM();", Format("if (true) { } M();", (Statements, "false")));
        }

        [Theory]
        [InlineData("true")]
        [InlineData("true:warning")]
        [InlineData("unset")]
        [InlineData("invalid")]
        [InlineData("")]
        public void Allowing_or_unrecognized_settings_preserve_existing_layout(string value)
        {
            const string Input = "class C\n{\nvoid M()\n{\nif (true) { }\nM();\n\n\nM();\n}\n\n}\n";
            Assert.Equal(CSharpFormatterTests.Format(Input), Format(Input, (Multiple, value), (Braces, value), (Statements, value)));
        }

        [Fact]
        public void Collapses_after_directives_and_documentation_without_changing_them()
        {
            const string Input = "#nullable enable\n\n\n/// <summary>C</summary>\n\n\nclass C { }\n";
            Assert.Equal("#nullable enable\n\n/// <summary>C</summary>\n\nclass C { }\n", Format(Input, (Multiple, "false")));
        }

        [Fact]
        public void Collapses_repeated_blank_lines_throughout_code_and_around_comments()
        {
            const string Input = "\n\n\nM();\n \n\t\nM();\n\n\n// explanation\n\n\nM();\n\n\n";
            Assert.Equal("\nM();\n\nM();\n\n// explanation\n\nM();\n\n",
                Format(Input, (Multiple, "false"), ("trim_trailing_whitespace", "true")));
            var result = Format("class C\n{\nvoid M()\n{\nA();\n\n\nB();\n}\n\n\nvoid N() { }\n}", (Multiple, "false"));
            Assert.Contains("A();\n\n        B();", result);
            Assert.Contains("}\n\n    void N()", result);
        }

        [Fact]
        public void Consecutive_braces_and_unclosed_exclusions_remain_protected()
        {
            const string Input = "class C\n{\n// pnfmt: off\nvoid M(){\nif(true){}\nM();\n\n\n}\n\n}";
            Assert.Equal(Input, Format(Input, (Multiple, "false"), (Braces, "false"), (Statements, "false")));
        }

        [Theory]
        [InlineData("if (true) { } else { }")]
        [InlineData("try { } catch { } finally { }")]
        [InlineData("do { } while (true);\nM();")]
        [InlineData("var x = new C { };\nM();")]
        [InlineData("if (true) { }\n// explanation\nM();")]
        [InlineData("if (true) { }\n#pragma warning disable\nM();")]
        [InlineData("if (true) { }\n\nM();")]
        [InlineData("class C\n{\nvoid M() { }\nvoid N() { }\n}")]
        public void Does_not_separate_continuations_declarations_or_protected_boundaries(string input)
        {
            Assert.Equal(CSharpFormatterTests.Format(input), Format(input, (Statements, "false")));
        }

        [Theory]
        [InlineData("pnfmt_format")]
        [InlineData("pnfmt_csharp_format")]
        public void Layout_switch_disables_new_preferences(string setting)
        {
            const string Input = "class C{\nvoid M(){\nif(true){}\nM();\n\n\nM();\n}\n\n}";
            Assert.Equal(Input, Format(Input, (Multiple, "false"), (Braces, "false"), (Statements, "false"), (setting, "false")));
        }

        [Theory]
        [InlineData("lf", "\n")]
        [InlineData("crlf", "\r\n")]
        [InlineData("cr", "\r")]
        public void Preferences_compose_and_accept_severity_and_configured_newlines(string style, string newline)
        {
            const string Input = "class C\n{\nvoid M()\n{\nif (true) { }\nM();\n\n\nM();\n}\n\n}\n";
            var result = Format(Input, (Multiple, " FALSE :warning"), (Braces, "false:suggestion"), (Statements, "false:none"),
                ("end_of_line", style), ("pnfmt_csharp_collapse_blank_lines", "true"));
            Assert.Contains("}" + newline + newline + "        M();", result);
            Assert.Contains("M();" + newline + newline + "        M();", result);
            Assert.EndsWith("    }" + newline + "}" + newline, result);
        }

        [Fact]
        public void Preserves_literal_comment_inactive_and_excluded_contents()
        {
            const string Protected = "// pnfmt: off\nif(true){ }\nM();\n\n\nM();\n// pnfmt: on\n";
            const string Inactive = "#if FEATURE\nif(true){ }\nM();\n\n\nM();\n#endif\n";
            const string Input = "var x = @\"one\n\n\nthree\";\nvar y = \"\"\"\nraw\n\n\ntext\n\"\"\";\n/* one\n\n\nthree */\n";
            var result = Format(Input + Protected + Inactive + "if (true) { }\nM();\n",
                (Multiple, "false"), (Braces, "false"), (Statements, "false"));
            Assert.StartsWith(Input, result);
            Assert.Contains(Protected, result);
            Assert.Contains(Inactive, result);
            Assert.EndsWith("if (true) { }\n\nM();\n", result);
        }

        [Fact]
        public void Removes_blank_lines_between_consecutive_closing_braces_only()
        {
            var result = Format("class C\n{\nvoid M()\n{\nif (true)\n{\nM();\n}\n\n\n}\n\n}\n", (Braces, "false"));
            Assert.Contains("M();\n        }\n    }\n}", result);
            const string Comments = "class C\n{\nvoid M()\n{\n}\n\n// keep\n\n}\n";
            Assert.Equal(CSharpFormatterTests.Format(Comments), Format(Comments, (Braces, "false")));
            Assert.Equal("class C { void M() { } }", Format("class C { void M() { } }", (Braces, "false")));
        }

        [Theory]
        [InlineData("if (true) { }", "M();")]
        [InlineData("if (true) { } else { }", "M();")]
        [InlineData("while (true) { }", "M();")]
        [InlineData("for (;;) { }", "M();")]
        [InlineData("foreach (var x in xs) { }", "M();")]
        [InlineData("try { } catch { } finally { }", "M();")]
        [InlineData("using (resource) { }", "M();")]
        [InlineData("lock (gate) { }", "M();")]
        [InlineData("checked { }", "M();")]
        [InlineData("unsafe { }", "M();")]
        [InlineData("switch (x) { default: break; }", "M();")]
        [InlineData("void Local() { }", "M();")]
        [InlineData("{ }", "{ }")]
        public void Separates_completed_blocks_from_following_statements(string block, string next)
        {
            Assert.Contains("}\n\n" + next, Format(block + "\n" + next, (Statements, "false")));
        }

        [Fact]
        public void Separates_statements_in_methods_and_switch_sections_and_after_trailing_comments()
        {
            var result = Format("class C\n{\nvoid M()\n{\nif (true) { } // done\nreturn;\n}\n}", (Statements, "false"));
            Assert.Contains("} // done\n\n        return;", result);
            result = Format("switch (x)\n{\ncase 0:\nif (true) { }\nbreak;\n}", (Statements, "false"));
            Assert.Contains("}\n\n        break;", result);
        }

        private static string Format(string input, params (string Key, string Value)[] options)
        {
            var result = CSharpFormatterTests.Format(input, options);
            Assert.Equal(result, CSharpFormatterTests.Format(result, options));
            Assert.Equal(CSharpSyntaxTree.ParseText(input).GetRoot().DescendantTokens().Select(token => token.Text),
                CSharpSyntaxTree.ParseText(result).GetRoot().DescendantTokens().Select(token => token.Text));
            return result;
        }
    }
}
