// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PNFmt
{
    internal static class CSharpRegionRemover
    {
        public static SyntaxNode Apply(SyntaxNode root, CSharpFormattingExclusions exclusions)
        {
            var source = SourceText.From(root.ToFullString());
            var starts = new Stack<DirectiveTriviaSyntax>();
            var changes = new List<TextChange>();
            foreach (var trivia in root.DescendantTrivia())
            {
                if (trivia.IsKind(SyntaxKind.RegionDirectiveTrivia))
                {
                    starts.Push((DirectiveTriviaSyntax)trivia.GetStructure());
                }
                else if (trivia.IsKind(SyntaxKind.EndRegionDirectiveTrivia) && starts.Count > 0)
                {
                    var start = starts.Pop();
                    var end = (DirectiveTriviaSyntax)trivia.GetStructure();
                    // Keep both ends when either one is inactive or protected. Removing
                    // only one would leave unbalanced directives in the source file.
                    if (CanRemove(start, source, exclusions) && CanRemove(end, source, exclusions))
                    {
                        changes.Add(new TextChange(source.Lines.GetLineFromPosition(start.SpanStart).SpanIncludingLineBreak, string.Empty));
                        changes.Add(new TextChange(source.Lines.GetLineFromPosition(end.SpanStart).SpanIncludingLineBreak, string.Empty));
                    }
                }
            }

            // Reparse so subsequent sorting and exclusion checks use the new positions.
            return changes.Count == 0 ? root : CSharpSyntaxTree.ParseText(
                source.WithChanges(changes.OrderBy(change => change.Span.Start)),
                (CSharpParseOptions)root.SyntaxTree.Options).GetRoot();
        }

        private static bool CanRemove(DirectiveTriviaSyntax directive, SourceText source, CSharpFormattingExclusions exclusions)
        {
            var line = source.Lines.GetLineFromPosition(directive.SpanStart);
            return directive.IsActive
                && !exclusions.Intersects(line.SpanIncludingLineBreak)
                && line.SpanIncludingLineBreak.Contains(directive.FullSpan)
                && string.IsNullOrWhiteSpace(source.ToString(TextSpan.FromBounds(line.Start, directive.SpanStart)));
        }
    }
}
