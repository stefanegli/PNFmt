// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PNFmt
{
    internal static class CSharpLineWrapper
    {
        public static string Apply(Document document, IReadOnlyDictionary<string, string> settings)
        {
            var source = document.GetTextAsync().GetAwaiter().GetResult();
            var width = PositiveInteger(settings, "max_line_length", 0);
            if (width == 0)
            {
                return source.ToString();
            }

            var tabWidth = PositiveInteger(settings, "tab_width", 4);
            settings.TryGetValue("indent_size", out var indentSizeSetting);
            var indentSize = indentSizeSetting == "tab" ? tabWidth : PositiveInteger(settings, "indent_size", 4);
            settings.TryGetValue("indent_style", out var indentStyle);
            var useTabs = string.Equals(indentStyle, "tab", StringComparison.OrdinalIgnoreCase);
            settings.TryGetValue("dotnet_style_operator_placement_when_wrapping", out var operatorPlacement);
            var operatorsAtEnd = string.Equals(operatorPlacement, "end_of_line", StringComparison.OrdinalIgnoreCase);
            settings.TryGetValue("end_of_line", out var endOfLine);
            var newLine = endOfLine == "crlf" ? "\r\n" : endOfLine == "lf" ? "\n" : endOfLine == "cr" ? "\r"
                : TextFileFormatting.DetectNewLine(source.ToString());

            // Token ordinals survive whitespace edits. Remember attempted boundaries so
            // Roslyn cannot cause a loop if a language formatting rule rejoins a line.
            var attempted = new HashSet<int>();
            while (true)
            {
                var root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
                var tokens = root.DescendantTokens().ToArray();
                var longLines = FindLongLines(source, tokens, width, tabWidth);
                if (!longLines.Any(value => value))
                {
                    return source.ToString();
                }

                var ordinals = tokens.Select((token, index) => (token, index))
                    .ToDictionary(item => item.token, item => item.index);
                var exclusions = CSharpFormattingExclusions.Parse(root);
                var changes = new Dictionary<int, TextChange>();
                var occupiedLines = new HashSet<int>();
                foreach (var group in Groups(root, operatorsAtEnd).OrderByDescending(item => item.Owner.Span.Length))
                {
                    if (exclusions.Intersects(group.Owner.Span))
                    {
                        continue;
                    }

                    var boundaries = group.Tokens.Where(token => !attempted.Contains(ordinals[token]))
                        .Select(token => (Token: token, Span: BreakBefore(token)))
                        .Where(item => item.Span.HasValue)
                        .Select(item => (item.Token, Span: item.Span.Value,
                            Line: source.Lines.GetLineFromPosition(item.Token.SpanStart).LineNumber))
                        .ToArray();
                    if (!boundaries.Any(item => longLines[item.Line])
                        || boundaries.Any(item => occupiedLines.Contains(item.Line)))
                    {
                        continue;
                    }

                    // Break an outer construct first, then measure its children again
                    // after indentation. This avoids wrapping short nested calls simply
                    // because their containing call was too long.
                    var indentation = ContinuationIndent(source, group.Owner.SpanStart, indentSize, tabWidth, useTabs);
                    foreach (var boundary in boundaries)
                    {
                        changes[boundary.Span.Start] = new TextChange(boundary.Span, newLine + indentation);
                        occupiedLines.Add(boundary.Line);
                        attempted.Add(ordinals[boundary.Token]);
                    }
                }

                if (changes.Count == 0)
                {
                    return source.ToString();
                }

                document = document.WithText(source.WithChanges(changes.Values.OrderBy(change => change.Span.Start)));
                document = Microsoft.CodeAnalysis.Formatting.Formatter.FormatAsync(document).GetAwaiter().GetResult();
                source = document.GetTextAsync().GetAwaiter().GetResult();
            }
        }

        private static IEnumerable<SyntaxToken> BinaryBreaks(BinaryExpressionSyntax binary, bool operatorsAtEnd)
        {
            if (binary.Left is BinaryExpressionSyntax left)
            {
                foreach (var token in BinaryBreaks(left, operatorsAtEnd))
                {
                    yield return token;
                }
            }
            yield return operatorsAtEnd ? binary.Right.GetFirstToken() : binary.OperatorToken;
            if (binary.Right is BinaryExpressionSyntax right)
            {
                foreach (var token in BinaryBreaks(right, operatorsAtEnd))
                {
                    yield return token;
                }
            }
        }

        private static TextSpan? BreakBefore(SyntaxToken token)
        {
            var previous = token.GetPreviousToken();
            if (previous.RawKind == 0 || previous.IsMissing || token.IsMissing
                || previous.TrailingTrivia.Concat(token.LeadingTrivia)
                    .Any(trivia => !trivia.IsKind(SyntaxKind.WhitespaceTrivia)))
            {
                return null;
            }

            return TextSpan.FromBounds(previous.Span.End, token.SpanStart);
        }

        private static IEnumerable<SyntaxToken> ChainBreaks(ExpressionSyntax expression)
        {
            // Follow only the receiver, never calls inside an argument or a lambda.
            if (expression is InvocationExpressionSyntax invocation)
            {
                if (invocation.Expression is MemberAccessExpressionSyntax member
                    && member.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                {
                    foreach (var token in ChainBreaks(member.Expression))
                    {
                        yield return token;
                    }
                    yield return member.OperatorToken;
                }
                else if (invocation.Expression is MemberBindingExpressionSyntax)
                {
                    // The enclosing conditional access owns the indivisible ?. pair.
                    yield break;
                }
            }
            else if (expression is ConditionalAccessExpressionSyntax conditional)
            {
                foreach (var token in ChainBreaks(conditional.Expression))
                {
                    yield return token;
                }
                yield return conditional.OperatorToken;
                foreach (var token in ChainBreaks(conditional.WhenNotNull))
                {
                    yield return token;
                }
            }
            else if (expression is MemberAccessExpressionSyntax access)
            {
                foreach (var token in ChainBreaks(access.Expression))
                {
                    yield return token;
                }
            }
        }

        private static string ContinuationIndent(SourceText source, int position, int indentSize, int tabWidth, bool useTabs)
        {
            var line = source.Lines.GetLineFromPosition(position);
            var column = 0;
            for (var index = line.Start; index < position; index++)
            {
                if (source[index] == '\t')
                {
                    column += tabWidth - column % tabWidth;
                }
                else if (source[index] == ' ')
                {
                    column++;
                }
                else
                {
                    break;
                }
            }

            column += indentSize;
            return useTabs ? new string('\t', column / tabWidth) + new string(' ', column % tabWidth)
                : new string(' ', column);
        }

        private static bool[] FindLongLines(SourceText source, SyntaxToken[] tokens, int width, int tabWidth)
        {
            // A long trailing comment does not make an otherwise short call wrap.
            // Multiline token interiors and directive/comment-only lines have no
            // candidate boundaries and need no width calculation.
            var codeEnds = new int[source.Lines.Count];
            foreach (var token in tokens.Where(item => item.Span.Length > 0))
            {
                var line = source.Lines.GetLineFromPosition(token.Span.End - 1);
                codeEnds[line.LineNumber] = token.Span.End;
            }

            var result = new bool[source.Lines.Count];
            foreach (var line in source.Lines)
            {
                long column = 0;
                for (var position = line.Start; position < codeEnds[line.LineNumber]; position++)
                {
                    column += source[position] == '\t' ? tabWidth - column % tabWidth : 1;
                    if (column > width)
                    {
                        result[line.LineNumber] = true;
                        break;
                    }
                }
            }
            return result;
        }

        private static IEnumerable<WrapGroup> Groups(SyntaxNode root, bool operatorsAtEnd)
        {
            foreach (var node in root.DescendantNodes(descendIntoChildren: node => !(node is InterpolatedStringExpressionSyntax)))
            {
                if (node is BaseArgumentListSyntax arguments)
                {
                    yield return new WrapGroup(node, arguments.Arguments.Select(argument => argument.GetFirstToken()).ToArray());
                }
                else if (node is BaseParameterListSyntax parameters)
                {
                    yield return new WrapGroup(node, parameters.Parameters.Select(parameter => parameter.GetFirstToken()).ToArray());
                }
                else if (node is BinaryExpressionSyntax binary && !(node.Parent is BinaryExpressionSyntax))
                {
                    yield return new WrapGroup(node, BinaryBreaks(binary, operatorsAtEnd).ToArray());
                }
                else if ((node is InvocationExpressionSyntax || node is ConditionalAccessExpressionSyntax
                    || node is MemberAccessExpressionSyntax)
                    && !(node.Parent is MemberAccessExpressionSyntax) && !(node.Parent is ConditionalAccessExpressionSyntax)
                    && !(node.Parent is InvocationExpressionSyntax invocation && invocation.Expression == node))
                {
                    var breaks = ChainBreaks((ExpressionSyntax)node).ToArray();
                    if (breaks.Length > 1 || (breaks.Length == 1 && HasInvocationReceiver(breaks[0])))
                    {
                        yield return new WrapGroup(node, breaks);
                    }
                }
            }
        }

        private static bool HasInvocationReceiver(SyntaxToken token)
        {
            var receiver = token.Parent is MemberAccessExpressionSyntax member ? member.Expression
                : (token.Parent as ConditionalAccessExpressionSyntax)?.Expression;
            while (receiver is MemberAccessExpressionSyntax access)
            {
                receiver = access.Expression;
            }
            return receiver is InvocationExpressionSyntax;
        }

        private static int PositiveInteger(IReadOnlyDictionary<string, string> settings, string name, int fallback)
        {
            return settings.TryGetValue(name, out var value)
                && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
                && result > 0 ? result : fallback;
        }

        private sealed class WrapGroup
        {
            public WrapGroup(SyntaxNode owner, SyntaxToken[] tokens)
            {
                this.Owner = owner;
                this.Tokens = tokens;
            }

            public SyntaxNode Owner { get; }
            public SyntaxToken[] Tokens { get; }
        }
    }
}
