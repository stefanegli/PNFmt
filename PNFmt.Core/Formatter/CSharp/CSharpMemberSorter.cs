// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PNFmt
{
    internal sealed class CSharpMemberSorter : CSharpSyntaxRewriter
    {
        public const string DefaultAccessibilityOrder = "public,internal,protected_internal,protected,private_protected,private";
        public const string DefaultOrder = "constant,constructor,destructor,property,indexer,event,method,operator,conversion_operator,type";

        private readonly Dictionary<string, int> accessibilityOrder;
        private readonly CSharpFormattingExclusions exclusions;
        private readonly string newLine;
        private readonly Dictionary<string, int> order;
        private readonly bool sortByName;

        public CSharpMemberSorter(IReadOnlyDictionary<string, string> settings, string newLine, CSharpFormattingExclusions exclusions)
        {
            this.exclusions = exclusions;
            this.newLine = newLine;
            this.sortByName = !settings.TryGetValue(EditorConfigSettingNames.CSharpSortMembersByName, out var byName)
                || string.Equals(byName, "unset", StringComparison.OrdinalIgnoreCase)
                || EditorConfigSettings.IsEnabled(byName);

            this.order = ReadOrder(settings, EditorConfigSettingNames.CSharpMemberOrder, DefaultOrder);
            this.accessibilityOrder = ReadOrder(settings, EditorConfigSettingNames.CSharpMemberAccessibilityOrder,
                DefaultAccessibilityOrder, allowNone: true);
            if (this.accessibilityOrder is null)
            {
                this.order = null;
            }
        }

        public override SyntaxNode Visit(SyntaxNode node)
        {
            if (this.order is null || node is ExtensionBlockDeclarationSyntax)
            {
                return node;
            }

            var visited = base.Visit(node);
            if (node is TypeDeclarationSyntax type)
            {
                // Attribute aliases cannot be resolved without a project. An attributed
                // interface may expose a COM vtable whose member order is significant.
                if (type is InterfaceDeclarationSyntax && type.AttributeLists.Count > 0)
                {
                    return visited;
                }

                return ((TypeDeclarationSyntax)visited).WithMembers(
                    this.Sort(type.Members, ((TypeDeclarationSyntax)visited).Members));
            }

            return visited;
        }

        private static string Accessibility(MemberDeclarationSyntax member)
        {
            if (member is ConstructorDeclarationSyntax constructor && constructor.Modifiers.Any(SyntaxKind.StaticKeyword)
                || member is MethodDeclarationSyntax method && method.ExplicitInterfaceSpecifier is not null
                || member is BasePropertyDeclarationSyntax property && property.ExplicitInterfaceSpecifier is not null)
            {
                return "private";
            }

            var modifiers = member.Modifiers;
            if (modifiers.Any(SyntaxKind.PublicKeyword))
            {
                return "public";
            }

            if (modifiers.Any(SyntaxKind.ProtectedKeyword))
            {
                return modifiers.Any(SyntaxKind.InternalKeyword) ? "protected_internal"
                    : modifiers.Any(SyntaxKind.PrivateKeyword) ? "private_protected" : "protected";
            }

            if (modifiers.Any(SyntaxKind.InternalKeyword))
            {
                return "internal";
            }

            if (modifiers.Any(SyntaxKind.PrivateKeyword))
            {
                return "private";
            }

            return member is DestructorDeclarationSyntax ? "protected"
                : member.Parent is InterfaceDeclarationSyntax ? "public" : "private";
        }

        private bool CanMove(MemberDeclarationSyntax member)
        {
            var category = Category(member);
            if (category is null || !this.order.ContainsKey(category)
                || (this.accessibilityOrder.Count > 0 && !this.accessibilityOrder.ContainsKey(Accessibility(member)))
                || this.exclusions.Intersects(member.FullSpan)
                || member.DescendantTrivia().Any(trivia => trivia.IsDirective || trivia.IsKind(SyntaxKind.DisabledTextTrivia))
                || member.DescendantNodes().OfType<AttributeSyntax>().Any(IsModuleInitializer)
                || member.GetLeadingTrivia().Any(trivia => !IsWhitespace(trivia)
                    && !trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                    && !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)))
            {
                return false;
            }

            if (member is PropertyDeclarationSyntax property)
            {
                // Storage order affects initialization and layout. Records also print
                // computed properties in declaration order in their synthesized ToString.
                return property.Parent is not RecordDeclarationSyntax
                    && property.Initializer is null
                    && !property.DescendantNodes().OfType<FieldExpressionSyntax>().Any()
                    && (property.Parent is InterfaceDeclarationSyntax
                        || property.Modifiers.Any(SyntaxKind.AbstractKeyword)
                        || property.Modifiers.Any(SyntaxKind.ExternKeyword)
                        || property.AccessorList is null
                        || property.AccessorList.Accessors.All(accessor => accessor.Body is not null || accessor.ExpressionBody is not null));
            }

            return true;
        }

        private static string Category(MemberDeclarationSyntax member)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field when field.Modifiers.Any(SyntaxKind.ConstKeyword): return "constant";
                case ConstructorDeclarationSyntax _: return "constructor";
                case DestructorDeclarationSyntax _: return "destructor";
                case PropertyDeclarationSyntax _: return "property";
                case IndexerDeclarationSyntax _: return "indexer";
                case EventDeclarationSyntax _: return "event";
                case MethodDeclarationSyntax _: return "method";
                case OperatorDeclarationSyntax _: return "operator";
                case ConversionOperatorDeclarationSyntax _: return "conversion_operator";
                case BaseTypeDeclarationSyntax _: return "type";
                case DelegateDeclarationSyntax _: return "type";
                default: return null;
            }
        }

        private static string ExplicitName(ExplicitInterfaceSpecifierSyntax specifier, string name)
        {
            return specifier is null ? name
                : string.Concat(specifier.Name.DescendantTokens().Select(token => token.ValueText)) + "." + name;
        }

        private static bool IsModuleInitializer(AttributeSyntax attribute)
        {
            // Moving a containing nested type can also change initializer order.
            var name = attribute.Name.GetLastToken().ValueText;
            return name == "ModuleInitializer" || name == "ModuleInitializerAttribute";
        }

        private static bool IsWhitespace(SyntaxTrivia trivia)
        {
            return trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia);
        }

        private static string Name(MemberDeclarationSyntax member)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field: return field.Declaration.Variables[0].Identifier.ValueText;
                case ConstructorDeclarationSyntax constructor: return constructor.Identifier.ValueText;
                case DestructorDeclarationSyntax destructor: return destructor.Identifier.ValueText;
                case PropertyDeclarationSyntax property: return ExplicitName(property.ExplicitInterfaceSpecifier, property.Identifier.ValueText);
                case IndexerDeclarationSyntax indexer: return ExplicitName(indexer.ExplicitInterfaceSpecifier, "this");
                case EventDeclarationSyntax @event: return ExplicitName(@event.ExplicitInterfaceSpecifier, @event.Identifier.ValueText);
                case MethodDeclarationSyntax method: return ExplicitName(method.ExplicitInterfaceSpecifier, method.Identifier.ValueText);
                case OperatorDeclarationSyntax @operator: return @operator.OperatorToken.ValueText;
                case ConversionOperatorDeclarationSyntax conversion: return conversion.ImplicitOrExplicitKeyword.ValueText;
                case BaseTypeDeclarationSyntax type: return type.Identifier.ValueText;
                case DelegateDeclarationSyntax @delegate: return @delegate.Identifier.ValueText;
                default: return string.Empty;
            }
        }

        private static Dictionary<string, int> ReadOrder(
            IReadOnlyDictionary<string, string> settings, string key, string defaultOrder, bool allowNone = false)
        {
            var value = settings.TryGetValue(key, out var configured)
                && !string.Equals(configured, "unset", StringComparison.OrdinalIgnoreCase) ? configured : defaultOrder;
            if (allowNone && string.Equals(value.Trim(), "none", StringComparison.OrdinalIgnoreCase))
            {
                return new Dictionary<string, int>();
            }

            var names = value.Split(',').Select(name => name.Trim().ToLowerInvariant()).ToArray();
            var known = new HashSet<string>(defaultOrder.Split(','), StringComparer.Ordinal);
            return names.Any(name => !known.Contains(name)) || names.Distinct(StringComparer.Ordinal).Count() != names.Length
                ? null : names.Select((name, index) => new { name, index }).ToDictionary(item => item.name, item => item.index);
        }

        private SyntaxList<MemberDeclarationSyntax> Sort(
            SyntaxList<MemberDeclarationSyntax> original,
            SyntaxList<MemberDeclarationSyntax> visited)
        {
            // Decisions use original positions: visiting nested types may have changed
            // their lengths, while exclusion spans still refer to the input tree.
            var result = visited.ToArray();
            var start = 0;
            while (start < original.Count)
            {
                if (!this.CanMove(original[start]))
                {
                    start++;
                    continue;
                }

                var end = start + 1;
                while (end < original.Count && this.CanMove(original[end]))
                {
                    end++;
                }

                var indexes = Enumerable.Range(start, end - start).ToArray();
                var sorted = indexes.OrderBy(index => this.order[Category(original[index])])
                    .ThenBy(index => this.accessibilityOrder.Count == 0 ? 0 : this.accessibilityOrder[Accessibility(original[index])])
                    .ThenBy(index => this.sortByName ? Name(original[index]) : string.Empty, StringComparer.Ordinal)
                    .ToArray();
                if (!indexes.SequenceEqual(sorted))
                {
                    for (var offset = 0; offset < sorted.Length; offset++)
                    {
                        var member = visited[sorted[offset]];
                        // Keep the slot's blank lines, but carry documentation, attributes,
                        // body comments, and trailing comments with the declaration.
                        var leading = SyntaxFactory.TriviaList(original[start + offset].GetLeadingTrivia().TakeWhile(IsWhitespace))
                            .AddRange(member.GetLeadingTrivia().SkipWhile(IsWhitespace));
                        if (leading.Any(trivia => !IsWhitespace(trivia))
                            && !leading.TakeWhile(IsWhitespace).Any(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia)))
                        {
                            leading = leading.Insert(0, SyntaxFactory.EndOfLine(this.newLine));
                        }

                        member = member.WithLeadingTrivia(leading);
                        if (!member.GetTrailingTrivia().Any(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia)))
                        {
                            member = member.WithTrailingTrivia(member.GetTrailingTrivia().Add(SyntaxFactory.EndOfLine(this.newLine)));
                        }

                        result[start + offset] = member;
                    }
                }

                start = end;
            }

            return SyntaxFactory.List(result);
        }
    }
}
