// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PNFmt
{
    internal static class CSharpCallCompactor
    {
        public static Document Apply(Document document, int width, int tabWidth, bool preserveSwitchArmSpacing)
        {
            // Join only list boundaries. Multiline expressions, comments and directives
            // inside arguments are not candidates for flattening. Reconsider parents
            // after compacting their nested calls, using Roslyn's configured spacing.
            while (true)
            {
                var root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
                var source = document.GetTextAsync().GetAwaiter().GetResult();
                var exclusions = CSharpFormattingExclusions.Parse(root);
                var changed = false;
                foreach (var list in root.DescendantNodes(descendIntoChildren: node => !(node is InterpolatedStringExpressionSyntax))
                    .OfType<BaseArgumentListSyntax>().OrderBy(node => node.Span.Length))
                {
                    var opening = list.GetFirstToken();
                    var preceding = opening.GetPreviousToken();
                    var closing = list.GetLastToken();
                    var start = preceding.RawKind == 0 ? opening.SpanStart : preceding.SpanStart;
                    if (source.Lines.GetLineFromPosition(start).LineNumber == source.Lines.GetLineFromPosition(closing.Span.End).LineNumber
                        || exclusions.Intersects(TextSpan.FromBounds(start, closing.Span.End))
                        || list.Arguments.Any(argument => source.Lines.GetLineFromPosition(argument.SpanStart).LineNumber
                            != source.Lines.GetLineFromPosition(argument.Span.End).LineNumber))
                    {
                        continue;
                    }

                    // Include the delimiters, commas, and the gap before '(' or '['.
                    var boundaries = new[] { opening }.Concat(list.Arguments.Select(argument => argument.GetFirstToken()))
                        .Concat(list.Arguments.GetSeparators()).Append(closing);
                    var changes = new List<TextChange>();
                    var safe = true;
                    foreach (var token in boundaries)
                    {
                        var previous = token.GetPreviousToken();
                        if (previous.RawKind == 0 || previous.IsMissing || token.IsMissing
                            || previous.TrailingTrivia.Concat(token.LeadingTrivia)
                                .Any(trivia => !trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia)))
                        {
                            safe = false;
                            break;
                        }
                        var gap = TextSpan.FromBounds(previous.Span.End, token.SpanStart);
                        if (source.Lines.GetLineFromPosition(gap.Start).LineNumber != source.Lines.GetLineFromPosition(gap.End).LineNumber)
                        {
                            changes.Add(new TextChange(gap, " "));
                        }
                    }
                    if (!safe || changes.Count == 0)
                    {
                        continue;
                    }

                    var candidate = document.WithText(source.WithChanges(changes.OrderBy(change => change.Span.Start)));
                    candidate = CSharpLineWrapper.FormatDocument(candidate, preserveSwitchArmSpacing);
                    var candidateSource = candidate.GetTextAsync().GetAwaiter().GetResult();
                    var candidateRoot = candidate.GetSyntaxRootAsync().GetAwaiter().GetResult();
                    // Tokens are unchanged; their ordinal identifies the same closing
                    // delimiter even when indentation shifts during formatting.
                    var ordinal = root.DescendantTokens().TakeWhile(token => token != closing).Count();
                    var candidateClosing = candidateRoot.DescendantTokens().ElementAt(ordinal);
                    var firstLine = candidateSource.Lines.GetLineFromPosition(start).LineNumber;
                    var lastLine = candidateSource.Lines.GetLineFromPosition(candidateClosing.SpanStart).LineNumber;
                    if (firstLine != lastLine || !Fits(candidateSource, candidateRoot, lastLine, width, tabWidth)
                        || candidateSource.ContentEquals(source))
                    {
                        continue;
                    }
                    document = candidate;
                    changed = true;
                    break;
                }
                if (!changed)
                {
                    return document;
                }
            }
        }

        private static bool Fits(SourceText source, SyntaxNode root, int lineNumber, int width, int tabWidth)
        {
            var line = source.Lines[lineNumber];
            var last = root.DescendantTokens().Last(token => token.Span.Length > 0 && token.SpanStart < line.End);
            // Count the suffix (including );), but not a trailing comment.
            if (last.Span.End > line.End)
            {
                return false;
            }
            long column = 0;
            for (var position = line.Start; position < last.Span.End; position++)
            {
                column += source[position] == '\t' ? tabWidth - column % tabWidth : 1;
            }
            return column <= width;
        }
    }
}
