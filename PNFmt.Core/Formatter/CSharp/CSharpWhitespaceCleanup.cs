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
            var newLine = ResolveNewLine(settings);
            var protectedIndex = 0;
            foreach (var line in source.Lines)
            {
                var changeSpan = GetLineEndSpan(text, line, trim);
                var overlapsProtectedSpan = OverlapsProtectedSpan(changeSpan, protectedSpans, ref protectedIndex);
                if (changeSpan.Length == 0 || exclusions.Intersects(changeSpan) || overlapsProtectedSpan)
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

        private static TextSpan GetLineEndSpan(string text, TextLine line, bool trim)
        {
            var end = line.End;
            if (trim)
            {
                while (end > line.Start && (text[end - 1] == ' ' || text[end - 1] == '\t'))
                {
                    end--;
                }
            }

            return TextSpan.FromBounds(end, line.EndIncludingLineBreak);
        }

        private static bool OverlapsProtectedSpan(TextSpan changeSpan, TextSpan[] protectedSpans, ref int index)
        {
            // Lines are visited in order, so consumed spans never need revisiting.
            while (index < protectedSpans.Length && protectedSpans[index].End <= changeSpan.Start)
            {
                index++;
            }

            return index < protectedSpans.Length && protectedSpans[index].OverlapsWith(changeSpan);
        }

        private static string ResolveNewLine(IReadOnlyDictionary<string, string> settings)
        {
            settings.TryGetValue("end_of_line", out var endOfLine);
            return endOfLine == "crlf" ? "\r\n" : endOfLine == "lf" ? "\n" : endOfLine == "cr" ? "\r" : null;
        }
    }
}
