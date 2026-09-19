// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace PNFmt
{
    internal static class CSharpFileHeader
    {
        public static string Apply(string text, IReadOnlyDictionary<string, string> settings, string filePath)
        {
            if (!settings.TryGetValue("file_header_template", out var template)
                || string.IsNullOrWhiteSpace(template)
                || string.Equals(template.Trim(), "unset", StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }

            var expected = NormalizeNewLines(template.Replace("\\n", "\n"))
                .Replace("{fileName}", Path.GetFileName(filePath));
            var root = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.CSharp14)).GetRoot();
            var comments = FindHeader(root);
            if (comments.Count > 0 && HeaderContents(comments) == NormalizeContents(expected))
            {
                return text;
            }

            var start = comments.Count > 0 ? comments[0].SpanStart : 0;
            var end = comments.Count > 0 ? comments[comments.Count - 1].FullSpan.End : 0;
            // Consume only surrounding whitespace. Later comment groups, documentation,
            // directives, and code stay in place after the replacement header.
            while (start > 0 && (text[start - 1] == ' ' || text[start - 1] == '\t'))
            {
                start--;
            }
            if (string.IsNullOrWhiteSpace(text.Substring(0, start)))
            {
                start = 0;
            }
            while (end < text.Length && char.IsWhiteSpace(text[end]))
            {
                end++;
            }

            var span = TextSpan.FromBounds(start, end);
            if (CSharpFormattingExclusions.Parse(root).Intersects(span))
            {
                return text;
            }

            settings.TryGetValue("end_of_line", out var style);
            var newLine = style == "crlf" ? "\r\n" : style == "lf" ? "\n" : style == "cr" ? "\r" : TextFileFormatting.DetectNewLine(text);
            var header = string.Join(newLine, expected.Split('\n').Select(line =>
                string.IsNullOrWhiteSpace(line) ? "//" : "// " + line.TrimEnd()));
            var separator = end < text.Length ? newLine + newLine
                : text.EndsWith("\n", StringComparison.Ordinal) || text.EndsWith("\r", StringComparison.Ordinal)
                    || EditorConfigSettings.IsEnabled(settings, "insert_final_newline") ? newLine : string.Empty;
            return text.Substring(0, start) + header + separator + text.Substring(end);
        }

        private static List<SyntaxTrivia> FindHeader(SyntaxNode root)
        {
            var result = new List<SyntaxTrivia>();
            var lineBreaks = 0;
            foreach (var trivia in root.GetFirstToken(includeZeroWidth: true).LeadingTrivia)
            {
                if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                {
                    continue;
                }
                if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    if (++lineBreaks > 1 && result.Count > 0)
                    {
                        break;
                    }
                    continue;
                }
                if (trivia.IsDirective && result.Count == 0)
                {
                    lineBreaks = 0;
                    continue;
                }
                if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
                {
                    // Formatter markers are control comments, never a replaceable header.
                    var comment = trivia.ToFullString().TrimEnd();
                    if (comment == "// pnfmt: off" || comment == "// pnfmt: on")
                    {
                        break;
                    }
                    result.Add(trivia);
                    lineBreaks = 0;
                    continue;
                }
                if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) && result.Count == 0)
                {
                    result.Add(trivia);
                }
                break;
            }
            return result;
        }

        private static string HeaderContents(List<SyntaxTrivia> comments)
        {
            if (comments[0].IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                var comment = comments[0].ToFullString();
                return NormalizeContents(string.Join("\n", NormalizeNewLines(comment.Substring(2, comment.Length - 4)).Trim()
                    .Split('\n').Select(line => line.TrimStart(' ', '\t', '*'))));
            }
            return NormalizeContents(string.Join("\n", comments.Select(comment => comment.ToFullString().Substring(2))));
        }

        private static string NormalizeContents(string text)
        {
            return string.Join("\n", text.Split('\n').Select(line => line.Trim()));
        }

        private static string NormalizeNewLines(string text)
        {
            return text.Replace("\r\n", "\n").Replace('\r', '\n');
        }
    }
}
