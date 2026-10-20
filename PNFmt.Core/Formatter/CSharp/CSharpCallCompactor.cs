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
            var rejected = new HashSet<int>();
            while (true)
            {
                var root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
                var source = document.GetTextAsync().GetAwaiter().GetResult();
                var tokens = root.DescendantTokens().ToArray();
                var ordinals = tokens.Select((token, index) => (token, index))
                    .ToDictionary(item => item.token, item => item.index);
                var exclusions = CSharpFormattingExclusions.Parse(root);
                var batch = new List<Call>();
                var occupied = new HashSet<int>();
                foreach (var list in root.DescendantNodes(descendIntoChildren: node => !(node is InterpolatedStringExpressionSyntax))
                    .OfType<BaseArgumentListSyntax>().OrderBy(node => node.Span.Length))
                {
                    var opening = list.GetFirstToken();
                    var preceding = opening.GetPreviousToken();
                    var first = preceding.RawKind == 0 ? opening : preceding;
                    var closing = list.GetLastToken();
                    var firstLine = source.Lines.GetLineFromPosition(first.SpanStart).LineNumber;
                    var lastLine = source.Lines.GetLineFromPosition(closing.Span.End).LineNumber;
                    if (firstLine == lastLine || rejected.Contains(ordinals[closing])
                        || exclusions.Intersects(TextSpan.FromBounds(first.SpanStart, closing.Span.End))
                        || list.Arguments.Any(argument => source.Lines.GetLineFromPosition(argument.SpanStart).LineNumber
                            != source.Lines.GetLineFromPosition(argument.Span.End).LineNumber)
                        || Enumerable.Range(firstLine, lastLine - firstLine + 1).Any(occupied.Contains))
                    {
                        continue;
                    }

                    var changes = JoiningChanges(list, opening, closing, source);
                    if (changes is null || changes.Count == 0)
                    {
                        continue;
                    }

                    // Disjoint source lines cannot affect each other's width. Calls
                    // sharing a line remain sequential; parents follow their children.
                    batch.Add(new Call(ordinals[first], ordinals[closing], changes));
                    occupied.UnionWith(Enumerable.Range(firstLine, lastLine - firstLine + 1));
                }
                if (batch.Count == 0)
                {
                    return document;
                }

                var candidate = Join(document, source, batch, preserveSwitchArmSpacing);
                var candidateSource = candidate.GetTextAsync().GetAwaiter().GetResult();
                var candidateTokens = candidate.GetSyntaxRootAsync().GetAwaiter().GetResult().DescendantTokens().ToArray();
                var codeEnds = new int[candidateSource.Lines.Count];
                foreach (var token in candidateTokens.Where(token => token.Span.Length > 0))
                {
                    codeEnds[candidateSource.Lines.GetLineFromPosition(token.SpanStart).LineNumber] = token.Span.End;
                }

                var accepted = batch.Where(call =>
                {
                    var line = candidateSource.Lines.GetLineFromPosition(candidateTokens[call.FirstToken].SpanStart).LineNumber;
                    return line == candidateSource.Lines.GetLineFromPosition(candidateTokens[call.LastToken].SpanStart).LineNumber
                        && Fits(candidateSource, codeEnds[line], line, width, tabWidth);
                }).ToArray();
                if (accepted.Length == 0)
                {
                    // A rejected call must not hide a later call sharing its line.
                    rejected.UnionWith(batch.Select(call => call.LastToken));
                    continue;
                }

                // Restore rejected calls from the original source, then let Roslyn
                // apply configured spacing once for the successful batch.
                document = accepted.Length == batch.Count ? candidate
                    : Join(document, source, accepted, preserveSwitchArmSpacing);
                rejected.Clear();
            }
        }

        private static bool Fits(SourceText source, int codeEnd, int lineNumber, int width, int tabWidth)
        {
            var line = source.Lines[lineNumber];
            // Count suffix code (including );), but not a trailing comment. A
            // multiline token on the resulting line cannot be compacted safely.
            if (codeEnd > line.End)
            {
                return false;
            }
            long column = 0;
            for (var position = line.Start; position < codeEnd; position++)
            {
                column += source[position] == '\t' ? tabWidth - column % tabWidth : 1;
            }
            return column <= width;
        }

        private static Document Join(Document document, SourceText source, IEnumerable<Call> calls, bool preserveSwitchArmSpacing)
        {
            var changes = calls.SelectMany(call => call.Changes).OrderBy(change => change.Span.Start);
            return CSharpLineWrapper.FormatDocument(document.WithText(source.WithChanges(changes)), preserveSwitchArmSpacing);
        }

        private static List<TextChange> JoiningChanges(BaseArgumentListSyntax list, SyntaxToken opening, SyntaxToken closing, SourceText source)
        {
            // Join only list boundaries, never expressions, comments or directives.
            var boundaries = new[] { opening }.Concat(list.Arguments.Select(argument => argument.GetFirstToken()))
                .Concat(list.Arguments.GetSeparators()).Append(closing);
            var changes = new List<TextChange>();
            foreach (var token in boundaries)
            {
                var previous = token.GetPreviousToken();
                if (previous.RawKind == 0 || previous.IsMissing || token.IsMissing
                    || previous.TrailingTrivia.Concat(token.LeadingTrivia)
                        .Any(trivia => !trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia)))
                {
                    return null;
                }
                var gap = TextSpan.FromBounds(previous.Span.End, token.SpanStart);
                if (source.Lines.GetLineFromPosition(gap.Start).LineNumber != source.Lines.GetLineFromPosition(gap.End).LineNumber)
                {
                    changes.Add(new TextChange(gap, " "));
                }
            }
            return changes;
        }

        private sealed class Call
        {
            public Call(int firstToken, int lastToken, List<TextChange> changes)
            {
                this.FirstToken = firstToken;
                this.LastToken = lastToken;
                this.Changes = changes;
            }

            public int FirstToken { get; }
            public int LastToken { get; }
            public List<TextChange> Changes { get; }
        }
    }
}
