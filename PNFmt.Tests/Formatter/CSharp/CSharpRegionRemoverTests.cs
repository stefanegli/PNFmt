// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace PNFmt.Tests.Formatter.CSharp
{
    public sealed class CSharpRegionRemoverTests
    {
        [Theory]
        [InlineData("\n")]
        [InlineData("\r\n")]
        [InlineData("\r")]
        public void Removes_nested_region_lines_and_labels_without_changing_enclosed_text(string newLine)
        {
            const string Input = "// Header\n#region Outer\nclass C\n{\n    #region Inner // label\n"
                + "    void M() { /* keep */ } // keep too\n    #endregion Inner\n}\n#endregion Outer\n";
            const string Expected = "// Header\nclass C\n{\n    void M() { /* keep */ } // keep too\n}\n";
            Assert.Equal(Expected.Replace("\n", newLine), Remove(Input.Replace("\n", newLine)));
            Assert.Equal(CSharpFormatterTests.Format(Expected.Replace("\n", newLine)), Format(Input.Replace("\n", newLine)));
        }

        [Fact]
        public void Removes_regions_inside_method_bodies_and_between_tokens()
        {
            const string Input = "class\n#region Name\nC\n#endregion\n{\nvoid M() {\n#region Body\nM();\n#endregion\n}\n}\n";
            var result = Format(Input);
            Assert.DoesNotContain("#region", result);
            Assert.DoesNotContain("#endregion", result);
            Assert.Equal(Tokens(Input), Tokens(result));
        }

        [Theory]
        [InlineData("#region Empty\n#endregion", "")]
        [InlineData("#region Empty\n#endregion\n", "")]
        [InlineData("#region Wrapper\nclass C { }\n#endregion", "class C { }")]
        [InlineData("#region Wrapper\nclass C { }\n#endregion\n", "class C { }\n")]
        public void Handles_empty_files_and_final_directives_without_a_newline(string input, string expected)
        {
            Assert.Equal(expected, Format(input));
        }

        [Fact]
        public void Leaves_region_text_inside_literals_comments_and_documentation_unchanged()
        {
            const string Input = "// #region comment\n/// <summary>#endregion docs</summary>\nclass C {\n"
                + "string S = @\"\n#region literal\n#endregion\n\";\n"
                + "string R = \"\"\"\n#region raw literal\n#endregion\n\"\"\";\n"
                + "/*\n#region block comment\n#endregion\n*/\n}\n";
            Assert.Equal(Input, Remove(Input));
            var result = Format(Input);
            Assert.Equal(Tokens(Input), Tokens(result));
            Assert.Contains("// #region comment", result);
            Assert.Contains("#endregion docs", result);
            Assert.Contains("#region block comment", result);
        }

        [Fact]
        public void Inactive_regions_and_all_other_directives_remain_exact()
        {
            const string Inactive = "#if FEATURE\n#region Inactive\nthis is inactive source   \n#endregion\n#else\n";
            const string Active = "#region Active\nclass C { }\n#endregion\n";
            const string Prefix = "#define LOCAL\n#nullable enable\n#pragma warning disable CS0169\n";
            var input = Prefix + Inactive + Active + "#endif\n#line default\n";
            var result = Format(input);
            Assert.Equal(Prefix + Inactive + "class C { }\n#endif\n#line default\n", result);
            Assert.Equal(OtherProtectedTrivia(input), OtherProtectedTrivia(result));
        }

        [Fact]
        public void Active_wrapper_can_be_removed_around_an_inactive_region()
        {
            const string Inner = "#if false\n#region Inactive\ninvalid inactive code\n#endregion\n#endif\nclass C { }\n";
            Assert.Equal(Inner, Format("#region Outer\n" + Inner + "#endregion\n"));
        }

        [Theory]
        [InlineData("// pnfmt: off\n#region Keep\nclass C { }\n// pnfmt: on\n#endregion\n")]
        [InlineData("#region Keep\n// pnfmt: off\nclass C { }\n#endregion\n// pnfmt: on\n")]
        [InlineData("// pnfmt: off\n#region Keep\nclass C { }\n#endregion\n// pnfmt: on\n")]
        public void Preserves_both_directives_when_either_is_excluded(string input)
        {
            Assert.Equal(input, Remove(input));
            Assert.Equal(input, Format(input));
        }

        [Fact]
        public void Removes_independent_inner_pairs_while_retaining_a_protected_outer_pair()
        {
            const string Input = "// pnfmt: off\n#region Keep\n// pnfmt: on\n"
                + "#region Remove\nclass C { }\n#endregion\n#endregion\n";
            const string Expected = "// pnfmt: off\n#region Keep\n// pnfmt: on\nclass C { }\n#endregion\n";
            Assert.Equal(Expected, Format(Input));
        }

        [Fact]
        public void Excluded_content_inside_a_removed_wrapper_remains_exact_after_sorting()
        {
            const string Protected = "// pnfmt: off\r\nvoid   Keep(){ }  \r\n// pnfmt: on\r\n";
            var input = "#region Imports\nusing Z;\nusing A;\n#endregion\nclass C {\n#region Members\n"
                + Protected + "void Z() { }\nvoid B() { }\nvoid A() { }\n#endregion\n}\n";
            var result = Format(input, ("pnfmt_sort_entries", "true"), ("pnfmt_csharp_sort_members", "true"),
                ("trim_trailing_whitespace", "true"), ("end_of_line", "lf"));
            Assert.StartsWith("using A;\nusing Z;\n", result);
            Assert.Contains(Protected, result);
            Assert.Contains("void A() { }\n    void B()", result);
            Assert.DoesNotContain("#region", result);
        }

        [Fact]
        public void Protected_final_newline_survives_when_removal_exposes_the_exclusion_at_eof()
        {
            const string Protected = "// pnfmt: off\nclass C { }\n// pnfmt: on\n";
            Assert.Equal(Protected, Format("#region Wrapper\n" + Protected + "#endregion"));
        }

        [Fact]
        public void Removed_regions_no_longer_divide_import_or_member_sorting_runs()
        {
            const string Input = "#region Imports\nusing Z;\n#endregion\nusing A;\nclass C {\n"
                + "#region Members\nvoid Z() { }\n#endregion\nvoid A() { }\n}\n";
            var result = Format(Input, ("pnfmt_sort_entries", "true"), ("pnfmt_csharp_sort_members", "true"));
            Assert.StartsWith("using A;\nusing Z;\n", result);
            Assert.Contains("void A() { }\n    void Z()", result);
            Assert.DoesNotContain("#region", result);
        }

        [Theory]
        [InlineData("#region Unclosed\nclass C { }\n")]
        [InlineData("class C { }\n#endregion\n")]
        [InlineData("#region Pair\nclass C {\n#endregion\n")]
        public void Invalid_input_is_skipped_without_trying_to_repair_it(string input)
        {
            var result = CSharpDocumentFormatter.Format(input,
                new Dictionary<string, string> { ["pnfmt_csharp_remove_regions"] = "true" }, out var diagnostic);
            Assert.Equal(input, result);
            Assert.Equal("PNFMT002", diagnostic.Code);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("false")]
        [InlineData("unset")]
        [InlineData("invalid")]
        public void Region_removal_is_opt_in(string value)
        {
            const string Input = "#region Keep\nclass C { }\n#endregion\n";
            var settings = value is null ? new (string, string)[0] : new[] { ("pnfmt_csharp_remove_regions", value) };
            Assert.Equal(Input, CSharpFormatterTests.Format(Input, settings));
        }

        [Fact]
        public void Activation_is_case_insensitive()
        {
            Assert.Equal("class C { }\n", CSharpFormatterTests.Format("#region Remove\nclass C { }\n#endregion\n",
                ("pnfmt_csharp_remove_regions", "TRUE")));
        }

        private static string Remove(string text)
        {
            var root = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.CSharp14)).GetRoot();
            return CSharpRegionRemover.Apply(root, CSharpFormattingExclusions.Parse(root)).ToFullString();
        }

        private static string Format(string text, params (string Key, string Value)[] settings)
        {
            var enabled = settings.Concat(new[] { ("pnfmt_csharp_remove_regions", "true") }).ToArray();
            var result = CSharpFormatterTests.Format(text, enabled);
            Assert.Equal(result, CSharpFormatterTests.Format(result, enabled));
            return result;
        }

        private static string[] Tokens(string text) => CSharpSyntaxTree.ParseText(text,
            new CSharpParseOptions(LanguageVersion.CSharp14)).GetRoot().DescendantTokens().Select(token => token.Text).ToArray();

        private static string[] OtherProtectedTrivia(string text) => CSharpSyntaxTree.ParseText(text).GetRoot().DescendantTrivia()
            .Where(trivia => trivia.IsKind(SyntaxKind.DisabledTextTrivia) || (trivia.IsDirective
                && !trivia.IsKind(SyntaxKind.RegionDirectiveTrivia) && !trivia.IsKind(SyntaxKind.EndRegionDirectiveTrivia)))
            .Select(trivia => trivia.ToFullString()).ToArray();
    }
}
