// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace PNFmt.Tests.Formatter.CSharp
{
    public sealed class CSharpUsingSorterTests
    {
        [Fact]
        public void Sorts_imports_with_system_first_then_static_then_aliases()
        {
            const string Input = "using Z = Zeta.Type;\nusing static Zeta.Helpers;\nusing Beta;\nusing System.IO;\n"
                + "using A = Alpha.Type;\nusing Alpha;\nusing static System.Math;\nusing System;\nclass C { }\n";
            var result = Format(Input);

            Assert.Equal(new[] { "System", "System.IO", "Alpha", "Beta", "System.Math", "Zeta.Helpers", "Alpha.Type", "Zeta.Type" },
                Imports(result));
            Assert.Equal(result, Format(result));
        }

        [Fact]
        public void System_first_can_be_disabled_and_groups_can_be_separated()
        {
            var result = Format("using Zebra;\nusing System.IO;\nusing Alpha.Two;\nusing Alpha.One;\nclass C { }\n",
                ("dotnet_sort_system_directives_first", "false"), ("dotnet_separate_import_directive_groups", "true"));

            Assert.StartsWith("using Alpha.One;\nusing Alpha.Two;\n\nusing System.IO;\n\nusing Zebra;", result);
            Assert.Equal(result, Format(result, ("dotnet_sort_system_directives_first", "false"),
                ("dotnet_separate_import_directive_groups", "true")));
        }

        [Fact]
        public void Global_and_namespace_scopes_are_preserved()
        {
            const string Input = "global using Zebra;\nglobal using Alpha;\nusing Zeta;\nusing Beta;\n"
                + "namespace N;\nusing Zulu;\nusing Charlie;\nclass C { }\n";
            var result = Format(Input);
            var root = CSharpSyntaxTree.ParseText(result).GetCompilationUnitRoot();

            Assert.Equal(new[] { "Alpha", "Zebra", "Beta", "Zeta" }, root.Usings.Select(item => item.Name.ToString()));
            Assert.All(root.Usings.Take(2), item => Assert.True(item.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)));
            var ns = Assert.IsType<FileScopedNamespaceDeclarationSyntax>(Assert.Single(root.Members));
            Assert.Equal(new[] { "Charlie", "Zulu" }, ns.Usings.Select(item => item.Name.ToString()));
            Assert.Equal(result, Format(result));
        }

        [Fact]
        public void Block_namespaces_sort_their_own_imports()
        {
            var result = Format("namespace N { using Z; using A; class C { } }\nnamespace M { using Y; using B; }\n");
            Assert.Equal(new[] { "A", "Z", "B", "Y" }, Imports(result));
            Assert.Equal(result, Format(result));
        }

        [Fact]
        public void Header_and_section_comments_stay_anchored_and_trailing_comments_follow_imports()
        {
            const string Input = "// Copyright\n\nusing Zeta; // zeta comment\nusing Beta;\n"
                + "// Application imports\nusing Zebra;\nusing Alpha;\nclass C { }\n";
            var result = Format(Input);

            Assert.StartsWith("// Copyright\n\nusing Beta;\nusing Zeta; // zeta comment\n", result);
            Assert.Contains("// Application imports\nusing Alpha;\nusing Zebra;", result);
            Assert.Equal(result, Format(result));
        }

        [Fact]
        public void Directives_and_disabled_branches_are_boundaries()
        {
            const string Input = "using Z;\nusing Y;\n#if FEATURE\nusing D;\nusing C;\n#else\nusing B;\nusing A;\n#endif\n"
                + "using F;\nusing E;\n#pragma warning disable CS8019\nusing H;\nusing G;\nclass C { }\n";
            var result = Format(Input);

            Assert.StartsWith("using Y;\nusing Z;\n#if FEATURE\nusing D;\nusing C;\n#else\nusing A;\nusing B;\n#endif\n", result);
            Assert.Contains("using E;\nusing F;\n#pragma warning disable CS8019\nusing G;\nusing H;", result);
            Assert.Equal(result, Format(result));
        }

        [Fact]
        public void Comments_inside_imports_pin_the_import()
        {
            var result = Format("using Z;\nusing /* keep */ Y;\nusing C;\nusing A;\nclass C { }\n");
            Assert.Equal(new[] { "Z", "Y", "A", "C" }, Imports(result));
            Assert.Contains("/* keep */", result);
            Assert.Equal(result, Format(result));
        }

        [Fact]
        public void Sorting_does_not_remove_duplicates_or_reorder_members()
        {
            const string Input = "using Z;\nusing A;\nusing A;\nclass C\n{\nstatic int Z = 1;\nstatic int A = Z + 1;\n}\n";
            var result = Format(Input);
            Assert.Equal(new[] { "A", "A", "Z" }, Imports(result));
            Assert.True(result.IndexOf("int Z") < result.IndexOf("int A"));
        }

        [Fact]
        public void Sorting_is_separately_opt_in()
        {
            var result = CSharpFormatterTests.Format("using Z;\nusing A;\nclass C { }\n");
            Assert.Equal(new[] { "Z", "A" }, Imports(result));
        }

        private static string Format(string text, params (string Key, string Value)[] settings)
        {
            return CSharpFormatterTests.Format(text,
                new[] { ("pnfmt_sort_entries", "true") }.Concat(settings).ToArray());
        }

        private static IEnumerable<string> Imports(string text)
        {
            return CSharpSyntaxTree.ParseText(text).GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>()
                .Select(item => item.Name.ToString());
        }
    }
}
