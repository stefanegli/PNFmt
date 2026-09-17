// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PNFmt
{
    internal sealed class CSharpModifierSorter : CSharpSyntaxRewriter
    {
        private const string DefaultOrder = "public,private,protected,internal,file,static,extern,new,virtual,abstract,sealed,override,readonly,unsafe,required,volatile,async";
        private readonly CSharpFormattingExclusions exclusions;
        private readonly Dictionary<string, int> order;

        public CSharpModifierSorter(IReadOnlyDictionary<string, string> settings, CSharpFormattingExclusions exclusions)
        {
            this.exclusions = exclusions;
            var value = settings.TryGetValue("csharp_preferred_modifier_order", out var configured)
                && !string.Equals(configured, "unset", StringComparison.OrdinalIgnoreCase) ? configured : DefaultOrder;
            var names = value.Split(':')[0].Split(',').Select(name => name.Trim()).ToArray();
            var known = new HashSet<string>(DefaultOrder.Split(','), StringComparer.Ordinal) { "partial" };
            if (names.Any(name => !known.Contains(name)) || names.Distinct(StringComparer.Ordinal).Count() != names.Length)
            {
                return;
            }

            this.order = names.Select((name, index) => new { name, index }).ToDictionary(item => item.name, item => item.index);
        }

        public override SyntaxNode Visit(SyntaxNode node)
        {
            var visited = base.Visit(node);
            if (node is MemberDeclarationSyntax member)
            {
                return ((MemberDeclarationSyntax)visited).WithModifiers(this.Sort(member.Modifiers));
            }

            if (node is LocalFunctionStatementSyntax function)
            {
                return ((LocalFunctionStatementSyntax)visited).WithModifiers(this.Sort(function.Modifiers));
            }

            return visited;
        }

        private SyntaxTokenList Sort(SyntaxTokenList modifiers)
        {
            if (this.order is null || modifiers.Count < 2)
            {
                return modifiers;
            }

            var span = TextSpan.FromBounds(modifiers.First().SpanStart, modifiers.Last().Span.End);
            if (this.exclusions.Intersects(span)
                || modifiers.Any(token => token.ValueText != "partial" && !this.order.ContainsKey(token.ValueText))
                || modifiers.SelectMany(token => token.LeadingTrivia.Concat(token.TrailingTrivia))
                    .Where(trivia => span.OverlapsWith(trivia.Span))
                    .Any(trivia => !trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia)))
            {
                return modifiers;
            }

            // 'partial' must remain next to the declaration keyword, regardless of its
            // configured rank. Unknown/unranked modifiers (including ref) pin the list.
            var sorted = modifiers.OrderBy(token => token.ValueText == "partial" ? int.MaxValue : this.order[token.ValueText]).ToArray();
            return SyntaxFactory.TokenList(sorted.Select((token, index) => token
                .WithLeadingTrivia(modifiers[index].LeadingTrivia)
                .WithTrailingTrivia(modifiers[index].TrailingTrivia)));
        }
    }
}
