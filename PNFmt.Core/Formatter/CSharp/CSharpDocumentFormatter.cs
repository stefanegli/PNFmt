// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

namespace PNFmt
{
    internal static class CSharpDocumentFormatter
    {
        // Compose the language services once. Each call owns its workspace and settings,
        // so concurrently formatted files cannot affect one another's options.
        private static readonly Lazy<MefHostServices> Host = new Lazy<MefHostServices>(
            () => MefHostServices.Create(MefHostServices.DefaultAssemblies
                .Concat(new[] { typeof(CSharpFormattingOptions).Assembly }).Distinct()));

        public static string Format(
            string text,
            IReadOnlyDictionary<string, string> settings,
            out FormatterDiagnostic diagnostic)
        {
            var tree = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.CSharp14));
            var error = tree.GetDiagnostics().FirstOrDefault(item => item.Severity == DiagnosticSeverity.Error);
            if (error is not null)
            {
                diagnostic = new FormatterDiagnostic(
                    "PNFMT002",
                    $"C# formatting skipped: {error.Id}: {error.GetMessage()}",
                    error.Location.GetLineSpan().StartLinePosition.Line + 1);
                return text;
            }

            diagnostic = null;
            var sourceRoot = tree.GetRoot();
            var originalExclusions = CSharpFormattingExclusions.Parse(sourceRoot);
            if (EditorConfigSettings.IsEnabled(settings, EditorConfigSettingNames.CSharpRemoveRegions))
            {
                sourceRoot = CSharpRegionRemover.Apply(sourceRoot, originalExclusions);
            }

            // Only the region remover may delete directives. Every directive and inactive
            // text fragment remaining here must survive the rest of the pipeline exactly.
            var protectedTrivia = ProtectedTrivia(sourceRoot).ToArray();
            if (EditorConfigSettings.IsEnabled(settings, EditorConfigSettingNames.SortEntries))
            {
                sourceRoot = new CSharpUsingSorter(settings, TextFileFormatting.DetectNewLine(text),
                    CSharpFormattingExclusions.Parse(sourceRoot)).Visit(sourceRoot);
            }

            if (EditorConfigSettings.IsEnabled(settings, EditorConfigSettingNames.CSharpSortModifiers))
            {
                sourceRoot = new CSharpModifierSorter(settings, CSharpFormattingExclusions.Parse(sourceRoot)).Visit(sourceRoot);
            }

            if (EditorConfigSettings.IsEnabled(settings, EditorConfigSettingNames.CSharpSortMembers))
            {
                sourceRoot = new CSharpMemberSorter(settings, TextFileFormatting.DetectNewLine(text),
                    CSharpFormattingExclusions.Parse(sourceRoot)).Visit(sourceRoot);
            }

            var formatLayout = FileFormattingConfiguration.FormatLayout(settings, "csharp");
            var formattedText = formatLayout
                ? FormatWhitespace(sourceRoot, tree, text, settings)
                : sourceRoot.ToFullString();
            var exclusions = CSharpFormattingExclusions.Parse(sourceRoot);
            if (exclusions.Spans.Count > 0)
            {
                // Restore complete marked regions after formatting. A whitespace edit
                // may span the closing marker's newline and the following declaration's
                // indentation; dropping that whole edit would leave enabled code unformatted.
                formattedText = exclusions.Restore(formattedText);
                if (formattedText is null)
                {
                    diagnostic = new FormatterDiagnostic("PNFMT003", "C# formatting skipped because exclusion markers would change.", null);
                    return text;
                }
            }
            var result = formatLayout ? CSharpWhitespaceCleanup.Apply(formattedText, settings) : formattedText;
            if (EditorConfigSettings.IsEnabled(settings, EditorConfigSettingNames.CSharpCollapseBlankLines))
            {
                result = CSharpBlankLineCleanup.Apply(result);
            }
            result = PreserveFinalNewline(text, result, settings, exclusions, (CSharpParseOptions)tree.Options);

            // In particular, literals (including raw strings) and inactive #if text
            // must remain byte-for-byte identical as text. Fail closed if a formatting
            // service ever proposes a change to either.
            var resultTree = CSharpSyntaxTree.ParseText(result, (CSharpParseOptions)tree.Options);
            var resultRoot = resultTree.GetRoot();
            if (resultTree.GetDiagnostics().Any(item => item.Severity == DiagnosticSeverity.Error)
                || !sourceRoot.DescendantTokens().Select(token => token.Text)
                    .SequenceEqual(resultRoot.DescendantTokens().Select(token => token.Text))
                || !protectedTrivia.SequenceEqual(ProtectedTrivia(resultRoot))
                || !originalExclusions.HasSameText(CSharpFormattingExclusions.Parse(resultRoot)))
            {
                diagnostic = new FormatterDiagnostic(
                    "PNFMT003", "C# formatting skipped because protected source text would change.", null);
                return text;
            }

            return result;
        }

        private static string CreateConfiguration(string text, IReadOnlyDictionary<string, string> settings)
        {
            var configuration = new StringBuilder("is_global = true\n");
            configuration.AppendLine("indent_style = space");
            configuration.AppendLine("indent_size = 4");
            configuration.AppendLine("tab_width = 4");
            var newLine = TextFileFormatting.DetectNewLine(text);
            configuration.AppendLine("end_of_line = " + (newLine == "\r\n" ? "crlf" : newLine == "\r" ? "cr" : "lf"));
            foreach (var setting in settings.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                if (!string.Equals(setting.Value, "unset", StringComparison.OrdinalIgnoreCase))
                {
                    configuration.Append(setting.Key).Append(" = ").AppendLine(setting.Value);
                }
            }

            return configuration.ToString();
        }

        private static string FormatWhitespace(
            SyntaxNode sourceRoot,
            SyntaxTree tree,
            string text,
            IReadOnlyDictionary<string, string> settings)
        {
            using (var workspace = new AdhocWorkspace(Host.Value))
            {
                // These paths only identify in-memory documents; no project/config file
                // is read or written and no compilation or metadata references are needed.
                var directory = Path.Combine(Path.GetTempPath(), "PNFmtCSharp");
                var project = workspace.AddProject("Formatting", LanguageNames.CSharp)
                    .WithParseOptions(tree.Options);
                project = project.AddAnalyzerConfigDocument(
                    ".globalconfig",
                    SourceText.From(CreateConfiguration(text, settings)),
                    filePath: Path.Combine(directory, ".globalconfig")).Project;
                var document = project.AddDocument(
                    "Source.cs", sourceRoot, filePath: Path.Combine(directory, "Source.cs"));
                var formatted = Microsoft.CodeAnalysis.Formatting.Formatter.FormatAsync(document)
                    .GetAwaiter().GetResult();
                return CSharpLineWrapper.Apply(formatted, settings);
            }
        }

        private static string PreserveFinalNewline(
            string originalText,
            string result,
            IReadOnlyDictionary<string, string> settings,
            CSharpFormattingExclusions exclusions,
            CSharpParseOptions parseOptions)
        {
            if (originalText.Length > 0 && originalText[originalText.Length - 1] != '\n' && originalText[originalText.Length - 1] != '\r'
                && !EditorConfigSettings.IsEnabled(settings, "insert_final_newline"))
            {
                // Sorting can move the final import into the middle of the list,
                // where it needs a newline. Preserve the original EOF convention,
                // unless removing a region exposed a protected newline at EOF.
                var trimmed = result.TrimEnd('\r', '\n');
                var touchesExclusion = exclusions.Spans.Count > 0 && CSharpFormattingExclusions.Parse(
                    CSharpSyntaxTree.ParseText(result, parseOptions).GetRoot())
                    .Intersects(TextSpan.FromBounds(trimmed.Length, result.Length));
                if (!touchesExclusion)
                {
                    result = trimmed;
                }
            }

            return result;
        }

        private static IEnumerable<string> ProtectedTrivia(SyntaxNode root)
        {
            return root.DescendantTrivia().Where(trivia =>
                    trivia.IsKind(SyntaxKind.DisabledTextTrivia) || trivia.IsDirective)
                .Select(trivia => trivia.ToFullString());
        }
    }
}
