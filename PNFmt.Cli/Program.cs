// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using PNFmt;

namespace PNFmt.Cli
{
    public static class Program
    {
        private const string ToolName = "pnfmt";

        private static readonly int StatusColumnWidth =
            new[] { "updated", "unchanged", "skipped", "would-update", "failed" }
                .Max(x => x.Length) + 2;

        public static int Main(string[] args)
        {
            CommandLineOptions options;
            try
            {
                options = CommandLineOptions.Parse(args);
            }
            catch (CommandLineException ex)
            {
                Console.Error.WriteLine(ex.Message);
                PrintUsage(Console.Error);
                return 2;
            }

            if (options.ShowHelp)
            {
                PrintUsage(Console.Out);
                return 0;
            }

            if (options.ShowVersion)
            {
                PrintVersion();
                return 0;
            }

            var allFormatters = FormatterCatalog.CreateDefault();
            if (!TryCreateActiveRegistry(
                    allFormatters,
                    options.FormatterNames,
                    out var registry,
                    out var formatterError))
            {
                Console.Error.WriteLine(formatterError);
                PrintUsage(Console.Error);
                return 2;
            }

            var filePatternMatcher = new FilePatternMatcher(options.FilePatterns);
            var targets = new TargetFileResolver(
                registry,
                allFormatters,
                filePatternMatcher).Resolve(options.Paths, options.Recursive);
            var files = targets.Files;

            foreach (var error in targets.Errors)
            {
                Console.Error.WriteLine(error);
            }

            if (files.Count == 0)
            {
                Console.WriteLine("No supported files found.");
                return targets.Errors.Count > 0 ? 2 : 0;
            }

            var workingDirectory = Environment.CurrentDirectory;
            var changed = 0;
            var unchanged = 0;
            var skipped = 0;
            var failed = 0;
            var diagnosticCount = 0;
            var run = new FormattingRunner(registry).Run(
                files,
                !options.DryRun,
                options.Lint,
                options.MaxCpuCount);

            foreach (var outcome in run.Outcomes)
            {
                WriteLog(outcome, options.Verbose);
                if (outcome.Error is not null)
                {
                    failed++;
                    if (options.Verbose)
                    {
                        WriteStatus("failed", outcome.File, workingDirectory);
                    }

                    Console.Error.WriteLine(
                        $"Failed to format {outcome.File}: {outcome.Error.Message}");
                    if (options.Verbose)
                    {
                        Console.Error.WriteLine(outcome.Error);
                    }

                    continue;
                }

                var status = outcome.Result.Status;
                switch (status)
                {
                    case FileFormatStatus.Updated:
                        changed++;
                        break;

                    case FileFormatStatus.Unchanged:
                        unchanged++;
                        break;

                    case FileFormatStatus.Skipped:
                        skipped++;
                        break;
                }

                if (options.Verbose)
                {
                    var statusLabel = status == FileFormatStatus.Updated && options.DryRun
                        ? "would-update"
                        : status.ToString().ToLowerInvariant();
                    WriteStatus(statusLabel, outcome.File, workingDirectory);
                }

                foreach (var diagnostic in outcome.Result.Diagnostics)
                {
                    diagnosticCount++;
                    WriteDiagnostic(diagnostic, outcome.File, workingDirectory);
                }
            }

            var changeLabel = options.DryRun ? "Would update" : "Updated";
            var elapsed = run.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture);
            Console.WriteLine(
                $"Processed {files.Count} file(s) in {elapsed}s. {changeLabel} {changed}, "
                + $"unchanged {unchanged}, skipped {skipped}, failed {failed}"
                + (options.Lint ? $", diagnostics {diagnosticCount}." : "."));

            if (failed > 0 || targets.Errors.Count > 0)
            {
                return 2;
            }

            if (options.Check
                && (changed > 0 || (options.Lint && diagnosticCount > 0)))
            {
                return 1;
            }

            return 0;
        }

        private static void PrintVersion()
        {
            var version = typeof(Program).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
                .Split('+')[0]
                ?? "unknown";
            Console.WriteLine($"{ToolName} {version}");
        }

        private static void PrintUsage(TextWriter writer)
        {
            var formatterCatalog = FormatterCatalog.CreateDefault();
            var formatterNames = string.Join(
                ", ",
                formatterCatalog.Formatters.Select(formatter => formatter.Name));
            var fileExtensions = string.Join(
                ", ",
                formatterCatalog.Formatters.SelectMany(formatter => formatter.FileExtensions));

            writer.WriteLine($"Usage: {ToolName} [options] [<path> ...]");
            writer.WriteLine();
            writer.WriteLine("Options:");
            writer.WriteLine("  -r, --recursive   Recurse into subdirectories when a path is a directory.");
            writer.WriteLine("  -v, --verbose     Show detailed per-file logging and errors.");
            writer.WriteLine("  -m[:N], -maxCpuCount[:N]");
            writer.WriteLine("                     Process up to N files concurrently; omit N to use all CPUs.");
            writer.WriteLine("      --file-pattern <glob>");
            writer.WriteLine("                     Include only files matching the glob; repeat to include more.");
            writer.WriteLine("      --formatter <name>[,<name>...]");
            writer.WriteLine("                     Enable only the named formatters; repeat or use comma-separated names.");
            writer.WriteLine($"                     Available names: {formatterNames}.");
            writer.WriteLine("  -n, --dry-run     Show what would change without writing files.");
            writer.WriteLine("      --check       Exit with code 1 if any file would change (implies --dry-run).");
            writer.WriteLine("      --lint        Report project diagnostics and formatting changes; exit 1 if found.");
            writer.WriteLine("  -h, --help        Show this help.");
            writer.WriteLine("  -V, --version     Show version info.");
            writer.WriteLine();
            writer.WriteLine("Notes:");
            writer.WriteLine("  If no path is provided, the current directory is used.");
            writer.WriteLine($"  Registered formatters support {fileExtensions} files.");
            writer.WriteLine("  Every formatter requires applicable EditorConfig settings.");
            writer.WriteLine("  INI formatting requires an enabled pnfmt_sort_entries or pnfmt_ini_* setting.");
            writer.WriteLine("  RSP and SLNX formatters require pnfmt_sort_entries = true.");
            writer.WriteLine("  Shared settings use pnfmt_; format-specific settings add the formatter name.");
            writer.WriteLine("  Legacy formatter settings remain fallbacks and produce warnings.");
        }

        private static bool TryCreateActiveRegistry(
            FormatterRegistry allFormatters,
            IReadOnlyCollection<string> requestedNames,
            out FormatterRegistry activeFormatters,
            out string error)
        {
            if (requestedNames.Count == 0)
            {
                activeFormatters = allFormatters;
                error = null;
                return true;
            }

            var availableNames = new HashSet<string>(
                allFormatters.Formatters.Select(formatter => formatter.Name),
                StringComparer.OrdinalIgnoreCase);
            var unknownNames = requestedNames
                .Where(name => !availableNames.Contains(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (unknownNames.Length > 0)
            {
                error = $"Unknown formatter(s): {string.Join(", ", unknownNames.Select(name => $"'{name}'"))}. "
                    + $"Available formatters: {string.Join(", ", allFormatters.Formatters.Select(formatter => formatter.Name))}.";
                activeFormatters = null;
                return false;
            }

            var requestedNameSet = new HashSet<string>(
                requestedNames,
                StringComparer.OrdinalIgnoreCase);
            activeFormatters = new FormatterRegistry(
                allFormatters.Formatters
                    .Where(formatter => requestedNameSet.Contains(formatter.Name))
                    .ToArray());
            error = null;
            return true;
        }

        private static void WriteStatus(string status, string file, string workingDirectory)
        {
            var statusLabel = $"[{status}]".PadRight(StatusColumnWidth);
            var displayPath = GetRelativePathFromWorkingDirectory(file, workingDirectory);
            Console.WriteLine($"{statusLabel} {displayPath}");
        }

        private static void WriteDiagnostic(
            FormatterDiagnostic diagnostic,
            string file,
            string workingDirectory)
        {
            var displayPath = GetRelativePathFromWorkingDirectory(file, workingDirectory);
            var location = diagnostic.LineNumber.HasValue
                ? $"{displayPath}({diagnostic.LineNumber.Value})"
                : displayPath;
            Console.WriteLine($"{location}: warning {diagnostic.Code}: {diagnostic.Message}");
        }

        private static string GetRelativePathFromWorkingDirectory(string file, string workingDirectory)
        {
            var relative = Path.GetRelativePath(workingDirectory, Path.GetFullPath(file));
            return string.IsNullOrEmpty(relative) ? "." : relative;
        }

        private static void WriteLog(FileFormattingOutcome outcome, bool verbose)
        {
            foreach (var message in outcome.LogMessages)
            {
                if (message.IndexOf(": warning PNFMT", StringComparison.Ordinal) >= 0)
                {
                    Console.Error.WriteLine(message);
                    continue;
                }

                if (verbose && !IsRedundantFormatterMessage(message))
                {
                    Console.WriteLine(message);
                }
            }

            if (verbose)
            {
                foreach (var exception in outcome.LoggedExceptions)
                {
                    Console.Error.WriteLine(exception);
                }
            }
        }

        private static bool IsRedundantFormatterMessage(string message)
        {
            return message.StartsWith("Updating ", StringComparison.OrdinalIgnoreCase)
                || message.StartsWith("Would update ", StringComparison.OrdinalIgnoreCase)
                || message.StartsWith("Skipping ", StringComparison.OrdinalIgnoreCase)
                || message.StartsWith(
                    "Update was not required",
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
