// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit;

namespace PNFmt.Tests.Formatter.CSharp
{
    public sealed class CSharpMemberSorterTests
    {
        [Fact]
        public void Accessibility_includes_compound_and_implicit_modifiers()
        {
            const string Input = "class C {\nvoid Implicit() { }\nprivate void Private() { }\n"
                + "private protected void PrivateProtected() { }\nprotected void Protected() { }\n"
                + "protected internal void ProtectedInternal() { }\ninternal void Internal() { }\npublic void Public() { }\n}\n";

            Assert.Equal(new[] { "Public", "Internal", "ProtectedInternal", "Protected", "PrivateProtected", "Implicit", "Private" }, Names(Format(Input)));
            Assert.Equal(new[] { "Implicit", "Private", "PrivateProtected", "Protected", "ProtectedInternal", "Internal", "Public" },
                Names(Format(Input, ("pnfmt_csharp_member_accessibility_order", "private,private_protected,protected,protected_internal,internal,public"))));
            Assert.Equal(new[] { "Implicit", "Internal", "Private", "PrivateProtected", "Protected", "ProtectedInternal", "Public" },
                Names(Format(Input, ("pnfmt_csharp_member_accessibility_order", "none"))));
        }

        [Fact]
        public void Custom_kind_order_and_optional_name_sort_preserve_ties()
        {
            const string Input = "class C {\npublic int ZProperty => 1;\nvoid Z() { }\nvoid A() { }\npublic int AProperty => 2;\n}\n";
            Assert.Equal(new[] { "A", "Z", "AProperty", "ZProperty" }, Names(Format(Input, ("pnfmt_csharp_member_order", "method,property"))));
            Assert.Equal(new[] { "Z", "A", "ZProperty", "AProperty" }, Names(Format(Input,
                ("pnfmt_csharp_member_order", "method,property"), ("pnfmt_csharp_sort_members_by_name", "false"))));
        }

        [Fact]
        public void Default_order_groups_kinds_then_accessibility_then_names()
        {
            const string Input = "class C {\n"
                + "class Nested { }\nvoid Z() { }\npublic void B() { }\npublic void A(int x) { }\n"
                + "public void A() { }\nprivate int AProperty => 1;\npublic int ZProperty => 2;\n"
                + "C() { }\nconst int BConstant = 1, AConstant = 2;\n~C() { }\n"
                + "public event Action E { add { } remove { } }\npublic int this[int i] => i;\n"
                + "public static C operator +(C a, C b) => a;\npublic static implicit operator int(C c) => 1;\n}\n";

            var result = Format(Input);
            Assert.Equal(new[] { "BConstant", "C", "~C", "ZProperty", "AProperty", "this", "E", "A", "A", "B", "Z", "+", "implicit", "Nested" }, Names(result));
            var overloads = Members(result).OfType<MethodDeclarationSyntax>().Where(method => method.Identifier.ValueText == "A");
            Assert.Equal(new[] { 1, 0 }, overloads.Select(method => method.ParameterList.Parameters.Count));
            Assert.Contains("BConstant = 1, AConstant = 2", result);
            Assert.Equal(Tokens(Input).OrderBy(token => token, StringComparer.Ordinal), Tokens(result).OrderBy(token => token, StringComparer.Ordinal));
        }

        [Fact]
        public void Directives_inside_a_method_and_inactive_branches_are_preserved()
        {
            const string Input = "class C {\nvoid Z() { }\nvoid WithDirective() {\n#pragma warning disable CS0168\n}\n"
                + "void B() { }\nvoid A() { }\n#if FEATURE\nthis is inactive text\n#endif\nvoid Last() { }\n}\n";
            var result = Format(Input);
            Assert.Equal(new[] { "Z", "WithDirective", "A", "B", "Last" }, Names(result));
            Assert.Contains("#if FEATURE\nthis is inactive text\n#endif", result);
        }

        [Fact]
        public void Documentation_attributes_body_comments_and_trailing_comments_travel_with_members()
        {
            const string Input = "class C {\n"
                + "/// <summary>Z docs</summary>\n[Z]\nvoid Z() { /* Z body */ } // Z tail\n"
                + "/// <summary>A docs</summary>\n[A]\nvoid A() { /* A body */ } // A tail\n}\n";
            var result = Format(Input);
            Assert.Equal(new[] { "A", "Z" }, Names(result));
            var members = Members(result);
            foreach (var member in members)
            {
                var name = Name(member);
                Assert.Contains(name + " docs", member.GetLeadingTrivia().ToFullString());
                Assert.Contains("[" + name + "]", member.ToString());
                Assert.Contains(name + " body", member.ToString());
                Assert.Contains(name + " tail", member.GetTrailingTrivia().ToFullString());
            }
        }

        [Fact]
        public void Exclusions_use_original_positions_when_nested_sorting_changes_lengths()
        {
            const string Protected = "// pnfmt: off\nvoid   ZKeep(){ }\nvoid   AKeep(){ }\n// pnfmt: on\n";
            var result = Format("using Z; using A;\nclass C {\nclass Nested { void Z() { } void A() { } }\n"
                + Protected + "static public void Z(){ }\nstatic public void A(){ }\n}\n",
                ("pnfmt_sort_entries", "true"), ("pnfmt_csharp_sort_modifiers", "true"), ("pnfmt_csharp_collapse_blank_lines", "true"));
            Assert.Contains(Protected, result);
            Assert.Contains("public static void A()", result);
            Assert.Equal(new[] { "Nested", "ZKeep", "AKeep", "Z", "A" }, Names(result));
        }

        [Fact]
        public void Explicit_interface_members_use_qualified_names_and_private_accessibility()
        {
            const string Input = "interface C : IZ, IA {\nvoid IZ.Z() { }\nvoid IA.Z() { }\nvoid Z();\nvoid A();\n}\n";
            var members = Members(Format(Input)).OfType<MethodDeclarationSyntax>().ToArray();
            Assert.Equal(new[] { "A", "Z", "Z", "Z" }, members.Select(method => method.Identifier.ValueText));
            Assert.Equal(new[] { null, null, "IA", "IZ" }, members.Select(method => method.ExplicitInterfaceSpecifier?.Name.ToString()));
        }

        [Fact]
        public void Initialization_behavior_is_unchanged_across_fields_properties_and_events()
        {
            const string Input = "public class C {\n"
                + "private static string trace = string.Empty;\nprivate static int ZField = Next(\"F\");\n"
                + "public static string Run() => trace;\nprivate static int AProperty { get; } = Next(\"P\");\n"
                + "private static event System.Action E = Create();\nprivate static int AField = Next(\"L\");\n"
                + "private static System.Action Create() { Next(\"E\"); return () => { }; }\n"
                + "private static int Next(string s) { trace += s; return trace.Length; }\n}\n";
            Assert.Equal("FPEL", Run(Input));
            Assert.Equal("FPEL", Run(Format(Input)));
        }

        [Fact]
        public void Interface_and_abstract_properties_can_move_without_backing_fields()
        {
            Assert.Equal(new[] { "A", "Z", "M" }, Names(Format("interface C { void M(); int Z { get; } int A { get; } }")));
            Assert.Equal(new[] { "A", "Z", "M" }, Names(Format("abstract class C { void M() { } public abstract int Z { get; } public abstract int A { get; } }")));
        }

        [Theory]
        [InlineData("pnfmt_csharp_member_order", "method,method")]
        [InlineData("pnfmt_csharp_member_order", "field,method")]
        [InlineData("pnfmt_csharp_member_order", "")]
        [InlineData("pnfmt_csharp_member_accessibility_order", "public,public")]
        [InlineData("pnfmt_csharp_member_accessibility_order", "unknown")]
        [InlineData("pnfmt_csharp_member_accessibility_order", "")]
        public void Invalid_orders_disable_member_sorting(string key, string value)
        {
            Assert.Equal(new[] { "Z", "A" }, Names(Format("class C { void Z() { } void A() { } }", (key, value))));
        }

        [Fact]
        public void Member_sorting_requires_its_own_switch()
        {
            const string Input = "class C { void Z() { } void A() { } }";
            Assert.Equal(new[] { "Z", "A" }, Names(CSharpFormatterTests.Format(Input, ("pnfmt_sort_entries", "true"))));
            foreach (var value in new[] { "false", "unset", "invalid" })
            {
                Assert.Equal(new[] { "Z", "A" }, Names(CSharpFormatterTests.Format(Input, ("pnfmt_csharp_sort_members", value))));
            }
        }

        [Fact]
        public void Module_initializers_and_their_containing_types_stay_in_order()
        {
            const string Input = "public class C {\npublic static string Trace = string.Empty;\n"
                + "public class ZNested { [System.Runtime.CompilerServices.ModuleInitializer] public static void Init() { C.Trace += \"Z\"; } }\n"
                + "public class ANested { [System.Runtime.CompilerServices.ModuleInitializerAttribute] public static void Init() { C.Trace += \"A\"; } }\n"
                + "[System.Runtime.CompilerServices.ModuleInitializer] public static void ZInit() { Trace += \"Z\"; }\n"
                + "[System.Runtime.CompilerServices.ModuleInitializerAttribute] public static void AInit() { Trace += \"A\"; }\n"
                + "public static string Run() => Trace;\n}\n";
            Assert.Equal("ZAZA", Run(Input));
            Assert.Equal("ZAZA", Run(Format(Input)));
        }

        [Fact]
        public void Nested_types_are_sorted_recursively_but_top_level_types_and_enum_values_are_not()
        {
            const string Input = "class Z {\nclass ZNested { void Z() { } void A() { } }\nclass ANested { }\n"
                + "enum E { Z, A }\n}\nclass A { }\n";
            var result = Format(Input);
            var root = CSharpSyntaxTree.ParseText(result).GetRoot();
            Assert.Equal(new[] { "ANested", "E", "ZNested" }, Names(result));
            Assert.Equal(new[] { "Z", "A" }, ((CompilationUnitSyntax)root).Members.OfType<TypeDeclarationSyntax>().Select(type => type.Identifier.ValueText));
            Assert.Equal(new[] { "A", "Z" }, root.DescendantNodes().OfType<TypeDeclarationSyntax>()
                .Single(type => type.Identifier.ValueText == "ZNested").Members.Select(Name));
            Assert.Equal(new[] { "Z", "A" }, root.DescendantNodes().OfType<EnumDeclarationSyntax>().Single().Members.Select(member => member.Identifier.ValueText));
        }

        [Fact]
        public void Omitted_kinds_and_accessibilities_form_boundaries()
        {
            const string Input = "class C {\npublic void Z() { }\npublic void Y() { }\nint P => 1;\n"
                + "public void D() { }\npublic void CMethod() { }\nprivate void Private() { }\npublic void B() { }\npublic void A() { }\n}\n";
            Assert.Equal(new[] { "Y", "Z", "P", "CMethod", "D", "Private", "A", "B" }, Names(Format(Input,
                ("pnfmt_csharp_member_order", "method"), ("pnfmt_csharp_member_accessibility_order", "public"))));
        }

        [Fact]
        public void Order_values_accept_unset_and_case_insensitive_names()
        {
            const string Input = "class C { private void A() { } public void Z() { } }";
            Assert.Equal(new[] { "Z", "A" }, Names(Format(Input, ("pnfmt_csharp_member_order", "unset"),
                ("pnfmt_csharp_member_accessibility_order", "unset"), ("pnfmt_csharp_sort_members_by_name", "unset"))));
            Assert.Equal(new[] { "A", "Z" }, Names(Format(Input, ("pnfmt_csharp_member_order", " METHOD "),
                ("pnfmt_csharp_member_accessibility_order", " PRIVATE, PUBLIC "))));
        }

        [Fact]
        public void Record_computed_properties_and_attributed_interface_members_stay_in_order()
        {
            const string Record = "record C { public int Z => 1; public int A => 2; }";
            const string Interface = "[ComImport] interface C { void Z(); void A(); }";
            Assert.Equal(new[] { "Z", "A" }, Names(Format(Record)));
            Assert.Equal(new[] { "Z", "A" }, Names(Format(Interface)));
        }

        [Theory]
        [InlineData("\n")]
        [InlineData("\r\n")]
        [InlineData("\r")]
        public void Sorting_preserves_literals_and_the_final_newline_convention(string newLine)
        {
            var input = "class C {\nstring Z() => @\"Z\nline\";\nstring A() => \"A\";\n}".Replace("\n", newLine);
            var result = Format(input);
            Assert.Equal(new[] { "A", "Z" }, Names(result));
            Assert.Contains("@\"Z" + newLine + "line\"", result);
            Assert.EndsWith("}", result);
        }

        [Theory]
        [InlineData("class C")]
        [InlineData("struct C")]
        [InlineData("record C")]
        [InlineData("record struct C")]
        [InlineData("interface C")]
        public void Sorts_methods_inside_each_supported_type(string declaration)
        {
            Assert.Equal(new[] { "A", "Z" }, Names(Format(declaration + " {\nvoid Z() { }\nvoid A() { }\n}\n")));
        }

        [Fact]
        public void Standalone_comments_directives_and_exclusions_form_boundaries()
        {
            const string Protected = "// pnfmt: off\nvoid   ZKeep(){ }\nvoid   AKeep(){ }\n// pnfmt: on\n";
            const string Input = "class C {\nvoid Z() { }\nvoid Y() { }\n// Section\nvoid Header() { }\n"
                + "void D() { }\nvoid CMethod() { }\n#region Group\nvoid RegionHeader() { }\nvoid B() { }\nvoid A() { }\n"
                + "#endregion\nvoid RegionEnd() { }\n";
            var result = Format(Input + Protected + "void After() { }\n}\n");
            Assert.Equal(new[] { "Y", "Z", "Header", "CMethod", "D", "RegionHeader", "A", "B", "RegionEnd", "ZKeep", "AKeep", "After" }, Names(result));
            Assert.Contains(Protected, result);
            Assert.Contains("// Section\n    void Header()", result);
        }

        [Theory]
        [InlineData("int Storage;")]
        [InlineData("static readonly int Storage = Create();")]
        [InlineData("int Storage { get; set; }")]
        [InlineData("static int Storage { get; } = Create();")]
        [InlineData("int Storage { get => field; set => field = value; }")]
        [InlineData("event Action Storage;")]
        [InlineData("event Action Storage = Create();")]
        [InlineData("unsafe fixed int Storage[8];")]
        public void Storage_declarations_stay_in_their_original_slots(string storage)
        {
            var input = "class C {\nvoid Z() { }\nvoid Y() { }\n" + storage + "\nvoid B() { }\nvoid A() { }\n}\n";
            var members = Members(Format(input));
            Assert.Equal(new[] { "Y", "Z" }, members.Take(2).Select(Name));
            Assert.Equal(Tokens(storage), members[2].DescendantTokens().Select(token => token.Text));
            Assert.Equal(new[] { "A", "B" }, members.Skip(3).Select(Name));
        }

        private static string Format(string text, params (string Key, string Value)[] settings)
        {
            var enabled = settings.Concat(new[] { ("pnfmt_csharp_sort_members", "true") }).ToArray();
            var result = CSharpFormatterTests.Format(text, enabled);
            Assert.Equal(result, CSharpFormatterTests.Format(result, enabled));
            return result;
        }

        private static SyntaxList<MemberDeclarationSyntax> Members(string text)
        {
            return CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.CSharp14)).GetRoot()
                .DescendantNodes().OfType<TypeDeclarationSyntax>().First().Members;
        }

        private static string Name(MemberDeclarationSyntax member)
        {
            return member switch
            {
                BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
                MethodDeclarationSyntax method => method.Identifier.ValueText,
                ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
                DestructorDeclarationSyntax destructor => "~" + destructor.Identifier.ValueText,
                PropertyDeclarationSyntax property => property.Identifier.ValueText,
                FieldDeclarationSyntax field => field.Declaration.Variables[0].Identifier.ValueText,
                EventDeclarationSyntax @event => @event.Identifier.ValueText,
                IndexerDeclarationSyntax _ => "this",
                OperatorDeclarationSyntax @operator => @operator.OperatorToken.ValueText,
                ConversionOperatorDeclarationSyntax conversion => conversion.ImplicitOrExplicitKeyword.ValueText,
                _ => throw new InvalidOperationException(member.Kind().ToString()),
            };
        }

        private static string[] Names(string text) => Members(text).Select(Name).ToArray();

        private static string Run(string source)
        {
            var compilation = CSharpCompilation.Create("MemberOrder" + Guid.NewGuid().ToString("N"),
                new[] { CSharpSyntaxTree.ParseText(source) },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using (var stream = new MemoryStream())
            {
                var result = compilation.Emit(stream);
                Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
                return (string)Assembly.Load(stream.ToArray()).GetType("C").GetMethod("Run").Invoke(null, null);
            }
        }

        private static string[] Tokens(string text) => CSharpSyntaxTree.ParseText(text).GetRoot().DescendantTokens()
            .Where(token => !token.IsKind(SyntaxKind.EndOfFileToken)).Select(token => token.Text).ToArray();
    }
}
