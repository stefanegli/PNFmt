// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PNFmt
{
    internal sealed class CSharpUsingSorter : CSharpSyntaxRewriter
    {
        private readonly string newLine;
        private readonly bool separateGroups;
        private readonly bool systemFirst;
        private readonly CSharpFormattingExclusions exclusions;

        public CSharpUsingSorter(IReadOnlyDictionary<string, string> settings, string newLine, CSharpFormattingExclusions exclusions)
        {
            this.newLine = newLine;
            this.exclusions = exclusions;
            this.systemFirst = !settings.TryGetValue("dotnet_sort_system_directives_first", out var value)
                || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
            this.separateGroups = EditorConfigSettings.IsEnabled(settings, "dotnet_separate_import_directive_groups");
        }

        public override SyntaxNode VisitCompilationUnit(CompilationUnitSyntax node)
        {
            var usings = this.Sort(node.Usings);
            var visited = (CompilationUnitSyntax)base.VisitCompilationUnit(node);
            return visited.WithUsings(usings);
        }

        public override SyntaxNode VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
        {
            var usings = this.Sort(node.Usings);
            var visited = (FileScopedNamespaceDeclarationSyntax)base.VisitFileScopedNamespaceDeclaration(node);
            return visited.WithUsings(usings);
        }

        public override SyntaxNode VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            var usings = this.Sort(node.Usings);
            var visited = (NamespaceDeclarationSyntax)base.VisitNamespaceDeclaration(node);
            return visited.WithUsings(usings);
        }

        private bool CanMove(UsingDirectiveSyntax node)
        {
            return !this.exclusions.Intersects(node.FullSpan)
                && node.DescendantTrivia().Where(trivia => node.Span.Contains(trivia.Span)).All(IsWhitespace)
                && node.GetTrailingTrivia().All(trivia => IsWhitespace(trivia) || trivia.IsKind(SyntaxKind.SingleLineCommentTrivia));
        }

        private static int Category(UsingDirectiveSyntax node)
        {
            return node.Alias is not null ? 2 : node.StaticKeyword.IsKind(SyntaxKind.StaticKeyword) ? 1 : 0;
        }

        private static bool IsWhitespace(SyntaxTrivia trivia)
        {
            return trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia);
        }

        private static string RootName(UsingDirectiveSyntax node)
        {
            if (node.Alias is not null)
            {
                return string.Empty;
            }

            var name = SortName(node);
            if (name.StartsWith("global::", StringComparison.Ordinal))
            {
                name = name.Substring("global::".Length);
            }

            var separator = name.IndexOf('.');
            return separator < 0 ? name : name.Substring(0, separator);
        }

        private SyntaxList<UsingDirectiveSyntax> Sort(SyntaxList<UsingDirectiveSyntax> usings)
        {
            var result = new List<UsingDirectiveSyntax>();
            var start = 0;
            while (start < usings.Count)
            {
                if (!this.CanMove(usings[start]))
                {
                    result.Add(usings[start++]);
                    continue;
                }

                var end = start + 1;
                while (end < usings.Count && this.CanMove(usings[end])
                    && usings[end].GlobalKeyword.RawKind == usings[start].GlobalKeyword.RawKind
                    && usings[end].GetLeadingTrivia().All(IsWhitespace))
                {
                    end++;
                }

                var sorted = usings.Skip(start).Take(end - start)
                    .OrderBy(Category)
                    .ThenBy(item => this.systemFirst && RootName(item) == "System" ? 0 : 1)
                    .ThenBy(SortName, StringComparer.Ordinal).ToArray();
                for (var index = 0; index < sorted.Length; index++)
                {
                    // Headers and section comments remain at the start of their run.
                    // A comment at the end of an import travels with that import.
                    var leading = index == 0 ? usings[start].GetLeadingTrivia() : default;
                    if (index > 0 && this.separateGroups
                        && (Category(sorted[index - 1]) != Category(sorted[index])
                            || RootName(sorted[index - 1]) != RootName(sorted[index])))
                    {
                        leading = SyntaxFactory.TriviaList(SyntaxFactory.EndOfLine(this.newLine));
                    }

                    var item = sorted[index].WithLeadingTrivia(leading);
                    if (!item.GetTrailingTrivia().Any(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia)))
                    {
                        item = item.WithTrailingTrivia(item.GetTrailingTrivia().Add(SyntaxFactory.EndOfLine(this.newLine)));
                    }

                    result.Add(item);
                }

                start = end;
            }

            return SyntaxFactory.List(result);
        }

        private static string SortName(UsingDirectiveSyntax node)
        {
            return node.Alias?.Name.Identifier.ValueText
                ?? string.Concat(node.NamespaceOrType.DescendantTokens().Select(token => token.Text));
        }
    }
}
