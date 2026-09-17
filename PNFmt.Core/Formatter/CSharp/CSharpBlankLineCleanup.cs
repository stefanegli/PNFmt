// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PNFmt
{
    internal static class CSharpBlankLineCleanup
    {
        public static string Apply(string text)
        {
            var root = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.CSharp14)).GetRoot();
            var exclusions = CSharpFormattingExclusions.Parse(root);
            var changes = new List<TextChange>();
            foreach (var parent in root.DescendantNodesAndSelf())
            {
                var members = parent.ChildNodes().OfType<MemberDeclarationSyntax>()
                    .Where(member => !(member is GlobalStatementSyntax)).ToArray();
                for (var index = 1; index < members.Length; index++)
                {
                    var span = TextSpan.FromBounds(members[index - 1].Span.End, members[index].SpanStart);
                    var gap = text.Substring(span.Start, span.Length);
                    if (!string.IsNullOrWhiteSpace(gap) || exclusions.Intersects(span))
                    {
                        continue;
                    }

                    var breaks = Regex.Matches(gap, "\r\n|\r|\n");
                    if (breaks.Count > 2)
                    {
                        var lastBreak = breaks[breaks.Count - 1];
                        changes.Add(new TextChange(span,
                            breaks[0].Value + breaks[1].Value + gap.Substring(lastBreak.Index + lastBreak.Length)));
                    }
                }
            }

            return SourceText.From(text).WithChanges(changes).ToString();
        }
    }
}
