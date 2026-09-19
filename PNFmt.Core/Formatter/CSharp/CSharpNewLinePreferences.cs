// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PNFmt
{
    internal static class CSharpNewLinePreferences
    {
        public static string Apply(string text, IReadOnlyDictionary<string, string> settings)
        {
            var collapse = Disallows(settings, "dotnet_style_allow_multiple_blank_lines_experimental");
            var consecutiveBraces = Disallows(settings, "csharp_style_allow_blank_lines_between_consecutive_braces_experimental");
            var separateStatements = Disallows(settings, "dotnet_style_allow_statement_immediately_after_block_experimental");
            if (!collapse && !consecutiveBraces && !separateStatements)
            {
                return text;
            }

            if (collapse)
            {
                text = CollapseBlankLines(text);
            }

            var root = Parse(text);
            var exclusions = CSharpFormattingExclusions.Parse(root);
            var changes = new List<TextChange>();
            foreach (var token in root.DescendantTokens().Where(token => token.IsKind(SyntaxKind.CloseBraceToken)))
            {
                var next = token.GetNextToken();
                if (next.RawKind == 0)
                {
                    continue;
                }

                var gap = TextSpan.FromBounds(token.Span.End, next.SpanStart);
                if (exclusions.Intersects(gap))
                {
                    continue;
                }

                if (consecutiveBraces && next.IsKind(SyntaxKind.CloseBraceToken)
                    && string.IsNullOrWhiteSpace(text.Substring(gap.Start, gap.Length)))
                {
                    CollapseGap(text, gap, 1, changes);
                }
                else if (separateStatements && (token.Parent is BlockSyntax || token.Parent is SwitchStatementSyntax)
                    && token.TrailingTrivia.LastOrDefault().IsKind(SyntaxKind.EndOfLineTrivia)
                    && next.LeadingTrivia.All(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia)))
                {
                    var statement = next.Parent?.AncestorsAndSelf().OfType<StatementSyntax>().FirstOrDefault();
                    if (statement is not null && statement.GetFirstToken() == next)
                    {
                        changes.Add(new TextChange(new TextSpan(next.FullSpan.Start, 0),
                            token.TrailingTrivia.Last().ToFullString()));
                    }
                }
            }

            return SourceText.From(text).WithChanges(changes).ToString();
        }

        private static string CollapseBlankLines(string text)
        {
            var root = Parse(text);
            var exclusions = CSharpFormattingExclusions.Parse(root);
            var changes = new List<TextChange>();
            // Only ordinary whitespace trivia participates. Multiline tokens, comments,
            // directives, and inactive text split runs and retain their contents.
            var whitespace = root.DescendantTrivia().Where(trivia =>
                    trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                .Select(trivia => trivia.FullSpan).ToArray();
            for (var index = 0; index < whitespace.Length; index++)
            {
                var start = whitespace[index].Start;
                var end = whitespace[index].End;
                while (index + 1 < whitespace.Length && whitespace[index + 1].Start == end)
                {
                    end = whitespace[++index].End;
                }

                var span = TextSpan.FromBounds(start, end);
                if (!exclusions.Intersects(span))
                {
                    var startsLine = start == 0 || text[start - 1] == '\n' || text[start - 1] == '\r';
                    CollapseGap(text, span, startsLine ? 1 : 2, changes);
                }
            }

            return SourceText.From(text).WithChanges(changes).ToString();
        }

        private static void CollapseGap(string text, TextSpan span, int maximumBreaks, List<TextChange> changes)
        {
            var gap = text.Substring(span.Start, span.Length);
            var breaks = Regex.Matches(gap, "\r\n|\r|\n");
            if (breaks.Count > maximumBreaks)
            {
                // Keep the first line break(s) and the final line's indentation.
                var kept = breaks[maximumBreaks - 1];
                var last = breaks[breaks.Count - 1];
                changes.Add(new TextChange(span,
                    gap.Substring(0, kept.Index + kept.Length) + gap.Substring(last.Index + last.Length)));
            }
        }

        private static bool Disallows(IReadOnlyDictionary<string, string> settings, string key)
        {
            return settings.TryGetValue(key, out var value)
                && string.Equals(value.Split(':')[0].Trim(), "false", StringComparison.OrdinalIgnoreCase);
        }

        private static SyntaxNode Parse(string text)
        {
            return CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.CSharp14)).GetRoot();
        }
    }
}
