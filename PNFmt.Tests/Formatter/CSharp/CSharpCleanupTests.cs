// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace PNFmt.Tests.Formatter.CSharp
{
    public sealed class CSharpCleanupTests
    {
        [Theory]
        [InlineData("static public class C { }", "public static class C")]
        [InlineData("class C { readonly private static int X; }", "private static readonly int X")]
        [InlineData("class C { static public void M() { } }", "public static void M()")]
        [InlineData("class C { virtual public int P { get; } }", "public virtual int P")]
        [InlineData("class C { static public event Action E; }", "public static event Action E")]
        [InlineData("class C { static public event Action E { add { } remove { } } }", "public static event Action E")]
        [InlineData("class C { unsafe public delegate void D(); }", "public unsafe delegate void D()")]
        [InlineData("readonly public struct C { }", "public readonly struct C")]
        [InlineData("sealed public record C;", "public sealed record C")]
        [InlineData("class C { void M() { async static Task F() { } } }", "static async Task F()")]
        [InlineData("abstract public interface I { }", "public abstract interface I")]
        public void Orders_existing_modifiers_without_changing_the_token_inventory(string input, string expected)
        {
            var result = Format(input);
            Assert.Contains(expected, result);
            Assert.Equal(Tokens(input).OrderBy(token => token), Tokens(result).OrderBy(token => token));
            Assert.Equal(result, Format(result));
        }

        [Fact]
        public void Custom_order_accepts_a_severity_suffix_and_keeps_partial_last()
        {
            const string Input = "public static partial class C { static public partial void M(); }";
            var result = CSharpFormatterTests.Format(Input, ("pnfmt_csharp_sort_modifiers", "true"),
                ("csharp_preferred_modifier_order", "partial,static,public:warning"));
            Assert.Contains("static public partial class C", result);
            Assert.Contains("static public partial void M()", result);
        }

        [Theory]
        [InlineData("public,public,static")]
        [InlineData("public,unknown,static")]
        [InlineData("public")]
        [InlineData("")]
        public void Invalid_orders_or_unranked_modifiers_leave_the_list_unchanged(string order)
        {
            var result = CSharpFormatterTests.Format("static public class C { }", ("pnfmt_csharp_sort_modifiers", "true"),
                ("csharp_preferred_modifier_order", order));
            Assert.Contains("static public class C", result);
        }

        [Fact]
        public void Ref_modifiers_are_not_reordered()
        {
            Assert.Equal("public readonly ref struct C { }", Format("public readonly ref struct C { }"));
        }

        [Fact]
        public void Comments_and_directives_between_modifiers_prevent_sorting()
        {
            const string Input = "static /* deliberate */ public class C { }\nstatic\n#pragma warning disable CS0169\npublic class D { }\n";
            var result = Format(Input);
            Assert.Contains("static /* deliberate */ public class C", result);
            Assert.Contains("static\n#pragma warning disable CS0169\npublic class D", result);
        }

        [Fact]
        public void Declaration_headers_stay_in_place()
        {
            var result = Format("// Header\n[Attribute]\nstatic public class C { }\n");
            Assert.StartsWith("// Header\n[Attribute]\npublic static class C", result);
        }

        [Fact]
        public void Collapses_only_repeated_blank_lines_between_declarations()
        {
            const string Input = "class C\n{\nvoid A() { }\n\n\n\nvoid B()\n{\nM();\n\n\nM();\n}\n}\n\n\nclass D { }\n";
            var result = Format(Input);
            Assert.Contains("void A() { }\n\n    void B()", result);
            Assert.Contains("M();\n\n\n        M();", result);
            Assert.Contains("}\n\nclass D", result);
            Assert.Equal(result, Format(result));
        }

        [Fact]
        public void Blank_lines_in_strings_comments_and_preprocessor_regions_are_preserved()
        {
            const string Input = "class C\n{\nstring S = @\"one\n\n\nthree\";\n\n/* one\n\n\nthree */\n\nvoid A() { }\n"
                + "\n#if FEATURE\n\nvoid X() { }\n\n\nvoid Y() { }\n#endif\n\nvoid B() { }\n}\n";
            var result = Format(Input);
            Assert.Contains("@\"one\n\n\nthree\"", result);
            var comment = CSharpSyntaxTree.ParseText(result).GetRoot().DescendantTrivia()
                .Single(trivia => trivia.RawKind == (int)SyntaxKind.MultiLineCommentTrivia).ToString();
            Assert.Equal("/* one\n\n\nthree */", string.Join("\n", comment.Split('\n').Select(line => line.TrimStart())));
            Assert.Contains("void X() { }\n\n\nvoid Y() { }", result);
            Assert.Equal(result, Format(result));
        }

        [Fact]
        public void Top_level_statements_do_not_get_blank_line_cleanup()
        {
            Assert.Contains("M();\n\n\nM();", Format("M();\n\n\nM();\n"));
        }

        [Fact]
        public void Both_cleanups_are_opt_in()
        {
            var result = CSharpFormatterTests.Format("static public class C { }\n\n\nclass D { }\n");
            Assert.Contains("static public class C", result);
            Assert.Contains("\n\n\nclass D", result);
        }

        [Fact]
        public void All_cleanups_respect_exclusions_after_import_sorting_changes_positions()
        {
            const string Protected = "// pnfmt: off\nstatic public void Keep(){ }\n\n\nstatic public void AlsoKeep(){ }\n// pnfmt: on\n";
            const string Input = "using Z; using A;\nnamespace Outer {\nusing Z; using A;\nnamespace Inner {\nusing Y; using B;\nclass C{\n";
            var result = CSharpFormatterTests.Format(Input + Protected + "static public void Change(){ }\n}\n}\n}\n",
                ("pnfmt_sort_entries", "true"), ("pnfmt_csharp_sort_modifiers", "true"), ("pnfmt_csharp_collapse_blank_lines", "true"));
            Assert.Contains(Protected, result);
            Assert.Contains("public static void Change()", result);
            Assert.StartsWith("using A;\nusing Z;", result);
        }

        private static string[] Tokens(string text)
        {
            return CSharpSyntaxTree.ParseText(text).GetRoot().DescendantTokens().Select(token => token.Text).ToArray();
        }

        private static string Format(string text)
        {
            return CSharpFormatterTests.Format(text, ("pnfmt_csharp_sort_modifiers", "true"),
                ("pnfmt_csharp_collapse_blank_lines", "true"));
        }
    }
}
