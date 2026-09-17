// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace PNFmt
{
    internal sealed class CSharpFormattingExclusions
    {
        private readonly SourceText source;

        private CSharpFormattingExclusions(SourceText source, IReadOnlyList<TextSpan> spans)
        {
            this.source = source;
            this.Spans = spans;
        }

        public IReadOnlyList<TextSpan> Spans { get; }

        public bool HasSameText(CSharpFormattingExclusions other)
        {
            return this.Spans.Select(span => this.source.ToString(span))
                .SequenceEqual(other.Spans.Select(span => other.source.ToString(span)));
        }

        public bool Intersects(TextSpan span)
        {
            return this.Spans.Any(excluded => span.Length == 0
                ? excluded.Contains(span.Start) || (span.Start == this.source.Length && excluded.End == this.source.Length)
                : excluded.OverlapsWith(span));
        }

        public static CSharpFormattingExclusions Parse(SyntaxNode root)
        {
            var source = SourceText.From(root.ToFullString());
            var spans = new List<TextSpan>();
            var depth = 0;
            var start = 0;
            foreach (var trivia in root.DescendantTrivia().Where(item => item.IsKind(SyntaxKind.SingleLineCommentTrivia)))
            {
                var marker = trivia.ToString().TrimEnd();
                if (marker != "// pnfmt: off" && marker != "// pnfmt: on")
                {
                    continue;
                }

                var line = source.Lines.GetLineFromPosition(trivia.SpanStart);
                if (!string.IsNullOrWhiteSpace(source.ToString(TextSpan.FromBounds(line.Start, trivia.SpanStart))))
                {
                    continue;
                }

                if (marker == "// pnfmt: off")
                {
                    if (depth++ == 0)
                    {
                        start = line.Start;
                    }
                }
                else if (depth > 0 && --depth == 0)
                {
                    spans.Add(TextSpan.FromBounds(start, line.EndIncludingLineBreak));
                }
            }

            if (depth > 0)
            {
                spans.Add(TextSpan.FromBounds(start, source.Length));
            }

            return new CSharpFormattingExclusions(source, spans);
        }

        public string Restore(string formatted)
        {
            var other = Parse(CSharpSyntaxTree.ParseText(formatted, new CSharpParseOptions(LanguageVersion.CSharp14)).GetRoot());
            if (this.Spans.Count != other.Spans.Count)
            {
                return null;
            }

            return other.source.WithChanges(other.Spans.Select((span, index) =>
                new TextChange(span, this.source.ToString(this.Spans[index])))).ToString();
        }
    }
}
