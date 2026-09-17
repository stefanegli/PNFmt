// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace PNFmt
{
    internal static class CSharpWhitespaceCleanup
    {
        public static string Apply(string text, IReadOnlyDictionary<string, string> settings)
        {
            var root = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.CSharp14)).GetRoot();
            var exclusions = CSharpFormattingExclusions.Parse(root);
            var protectedSpans = root.DescendantTokens().Select(token => token.Span)
                .Concat(root.DescendantTrivia().Where(trivia =>
                    !trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                    .Select(trivia => trivia.FullSpan))
                .Where(span => span.Length > 0).OrderBy(span => span.Start).ToArray();
            var source = SourceText.From(text);
            var changes = new List<TextChange>();
            var trim = EditorConfigSettings.IsEnabled(settings, "trim_trailing_whitespace");
            settings.TryGetValue("end_of_line", out var endOfLine);
            var newLine = endOfLine == "crlf" ? "\r\n" : endOfLine == "lf" ? "\n" : endOfLine == "cr" ? "\r" : null;
            var protectedIndex = 0;
            foreach (var line in source.Lines)
            {
                var end = line.End;
                if (trim)
                {
                    while (end > line.Start && (text[end - 1] == ' ' || text[end - 1] == '\t'))
                    {
                        end--;
                    }
                }

                var changeSpan = TextSpan.FromBounds(end, line.EndIncludingLineBreak);
                while (protectedIndex < protectedSpans.Length && protectedSpans[protectedIndex].End <= changeSpan.Start)
                {
                    protectedIndex++;
                }

                if (changeSpan.Length == 0 || exclusions.Intersects(changeSpan)
                    || (protectedIndex < protectedSpans.Length && protectedSpans[protectedIndex].OverlapsWith(changeSpan)))
                {
                    continue;
                }

                var replacement = line.EndIncludingLineBreak == line.End
                    ? string.Empty
                    : newLine ?? text.Substring(line.End, line.EndIncludingLineBreak - line.End);
                if (source.ToString(changeSpan) != replacement)
                {
                    changes.Add(new TextChange(changeSpan, replacement));
                }
            }

            var result = source.WithChanges(changes).ToString();
            if (EditorConfigSettings.IsEnabled(settings, "insert_final_newline")
                && !exclusions.Intersects(new TextSpan(text.Length, 0))
                && result.Length > 0 && result[result.Length - 1] != '\n' && result[result.Length - 1] != '\r')
            {
                result += newLine ?? TextFileFormatting.DetectNewLine(text);
            }

            return result;
        }
    }
}
