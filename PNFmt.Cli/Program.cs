// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PNFmt;

namespace PNFmt.Cli
{
    public static class Program
    {
        private const string ToolName = "pnfmt";

        private static readonly HashSet<string> IgnoredRecursiveDirectories =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".git",
                ".vs",
                "artifacts",
                "bin",
                "node_modules",
                "obj",
            };

        private static readonly int StatusColumnWidth =
            new[] { "updated", "unchanged", "skipped", "would-update", "failed" }
                .Max(x => x.Length) + 2;

        public static int Main(string[] args)
        {
            var recursive = false;
            var verbose = false;
            var dryRun = false;
            var check = false;
            var lint = false;
            var stopOptions = false;
            var maxCpuCount = 1;
            var paths = new List<string>();
            var arguments = args ?? Array.Empty<string>();

            for (var index = 0; index < arguments.Length; index++)
            {
                var arg = arguments[index];
                if (!stopOptions && string.Equals(arg, "--", StringComparison.Ordinal))
                {
                    stopOptions = true;
                    continue;
                }

                if (!stopOptions && TryGetMaxCpuCountValue(arg, out var maxCpuCountValue))
                {
                    if (maxCpuCountValue is null)
                    {
                        maxCpuCount = Math.Max(1, Environment.ProcessorCount);
                    }
                    else if (!TryParseMaxCpuCount(maxCpuCountValue, out maxCpuCount))
                    {
                        Console.Error.WriteLine(
                            $"Option '{arg}' requires a positive integer after ':'.");
                        PrintUsage(Console.Error);
                        return 2;
                    }

                    continue;
                }

                if (!stopOptions && IsHelpArg(arg))
                {
                    PrintUsage(Console.Out);
                    return 0;
                }

                if (!stopOptions && IsVersionArg(arg))
                {
                    PrintVersion();
                    return 0;
                }

                if (!stopOptions && (string.Equals(arg, "-r", StringComparison.Ordinal)
                    || string.Equals(arg, "--recursive", StringComparison.Ordinal)))
                {
                    recursive = true;
                    continue;
                }

                if (!stopOptions && (string.Equals(arg, "-v", StringComparison.Ordinal)
                    || string.Equals(arg, "--verbose", StringComparison.Ordinal)))
                {
                    verbose = true;
                    continue;
                }

                if (!stopOptions && (string.Equals(arg, "-n", StringComparison.Ordinal)
                    || string.Equals(arg, "--dry-run", StringComparison.Ordinal)))
                {
                    dryRun = true;
                    continue;
                }

                if (!stopOptions && string.Equals(arg, "--check", StringComparison.Ordinal))
                {
                    check = true;
                    dryRun = true;
                    continue;
                }

                if (!stopOptions && string.Equals(arg, "--lint", StringComparison.Ordinal))
                {
                    lint = true;
                    check = true;
                    dryRun = true;
                    continue;
                }

                if (!stopOptions && arg.StartsWith("-", StringComparison.Ordinal))
                {
                    Console.Error.WriteLine($"Unknown option: {arg}");
                    PrintUsage(Console.Error);
                    return 2;
                }

                paths.Add(arg);
            }

            if (paths.Count == 0)
            {
                paths.Add(".");
            }

            var registry = FormatterCatalog.CreateDefault();
            var pathErrors = new List<string>();
            var files = ResolveTargetFiles(paths, recursive, registry, pathErrors);

            foreach (var error in pathErrors)
            {
                Console.Error.WriteLine(error);
            }

            if (files.Count == 0)
            {
                Console.WriteLine("No supported files found.");
                return pathErrors.Count > 0 ? 2 : 0;
            }

            var workingDirectory = Environment.CurrentDirectory;
            var changed = 0;
            var unchanged = 0;
            var skipped = 0;
            var failed = 0;
            var diagnosticCount = 0;
            var run = new FormattingRunner(registry).Run(files, !dryRun, lint, maxCpuCount);

            foreach (var outcome in run.Outcomes)
            {
                WriteLog(outcome, verbose);
                if (outcome.Error is not null)
                {
                    failed++;
                    if (verbose)
                    {
                        WriteStatus("failed", outcome.File, workingDirectory);
                    }

                    Console.Error.WriteLine(
                        $"Failed to format {outcome.File}: {outcome.Error.Message}");
                    if (verbose)
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

                if (verbose)
                {
                    var statusLabel = status == FileFormatStatus.Updated && dryRun
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

            var changeLabel = dryRun ? "Would update" : "Updated";
            var elapsed = run.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture);
            Console.WriteLine(
                $"Processed {files.Count} file(s) in {elapsed}s. {changeLabel} {changed}, "
                + $"unchanged {unchanged}, skipped {skipped}, failed {failed}"
                + (lint ? $", diagnostics {diagnosticCount}." : "."));

            if (failed > 0 || pathErrors.Count > 0)
            {
                return 2;
            }

            if (check && (changed > 0 || (lint && diagnosticCount > 0)))
            {
                return 1;
            }

            return 0;
        }

        private static bool TryGetMaxCpuCountValue(string arg, out string value)
        {
            const string ShortOption = "-m";
            const string LongOption = "-maxCpuCount";
            if (string.Equals(arg, ShortOption, StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, LongOption, StringComparison.OrdinalIgnoreCase))
            {
                value = null;
                return true;
            }

            var shortPrefix = ShortOption + ":";
            if (arg.StartsWith(shortPrefix, StringComparison.OrdinalIgnoreCase))
            {
                value = arg.Substring(shortPrefix.Length);
                return true;
            }

            var longPrefix = LongOption + ":";
            if (arg.StartsWith(longPrefix, StringComparison.OrdinalIgnoreCase))
            {
                value = arg.Substring(longPrefix.Length);
                return true;
            }

            value = null;
            return false;
        }

        private static bool TryParseMaxCpuCount(string value, out int maxCpuCount)
        {
            return int.TryParse(
                    value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out maxCpuCount)
                && maxCpuCount > 0;
        }

        private static bool IsHelpArg(string arg)
        {
            return string.Equals(arg, "-h", StringComparison.Ordinal)
                || string.Equals(arg, "--help", StringComparison.Ordinal)
                || string.Equals(arg, "/?", StringComparison.Ordinal);
        }

        private static bool IsVersionArg(string arg)
        {
            return string.Equals(arg, "-V", StringComparison.Ordinal)
                || string.Equals(arg, "--version", StringComparison.Ordinal);
        }

        private static void PrintVersion()
        {
            var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";
            Console.WriteLine($"{ToolName} {version}");
        }

        private static void PrintUsage(TextWriter writer)
        {
            writer.WriteLine($"Usage: {ToolName} [options] [<path> ...]");
            writer.WriteLine();
            writer.WriteLine("Options:");
            writer.WriteLine("  -r, --recursive   Recurse into subdirectories when a path is a directory.");
            writer.WriteLine("  -v, --verbose     Show detailed per-file logging and errors.");
            writer.WriteLine("  -m[:N], -maxCpuCount[:N]");
            writer.WriteLine("                     Process up to N files concurrently; omit N to use all CPUs.");
            writer.WriteLine("  -n, --dry-run     Show what would change without writing files.");
            writer.WriteLine("      --check       Exit with code 1 if any file would change (implies --dry-run).");
            writer.WriteLine("      --lint        Report project diagnostics and formatting changes; exit 1 if found.");
            writer.WriteLine("  -h, --help        Show this help.");
            writer.WriteLine("  -V, --version     Show version info.");
            writer.WriteLine();
            writer.WriteLine("Notes:");
            writer.WriteLine("  If no path is provided, the current directory is used.");
            writer.WriteLine("  Registered formatters support .csproj, .resx, .slnx, .editorconfig, and .ini files.");
            writer.WriteLine("  Project and resource formatting runs only when EditorConfig enables it.");
            writer.WriteLine("  Shared settings use pnfmt_; format-specific settings add csproj_ or resx_.");
            writer.WriteLine("  Legacy formatter settings remain fallbacks and produce warnings.");
        }

        private static List<string> ResolveTargetFiles(
            IEnumerable<string> paths,
            bool recursive,
            FormatterRegistry registry,
            List<string> errors)
        {
            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawPath in paths)
            {
                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    continue;
                }

                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(rawPath);
                }
                catch (Exception ex) when (IsPathException(ex))
                {
                    errors.Add($"Unable to access path '{rawPath}': {ex.Message}");
                    continue;
                }

                if (File.Exists(fullPath))
                {
                    if (registry.TryGetFormatter(fullPath, out _))
                    {
                        results.Add(fullPath);
                    }
                    else
                    {
                        errors.Add($"Path is not a supported file type: {fullPath}");
                    }

                    continue;
                }

                if (Directory.Exists(fullPath))
                {
                    CollectDirectoryFiles(fullPath, recursive, registry, results, errors);
                    continue;
                }

                errors.Add($"Path not found: {fullPath}");
            }

            return results.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void CollectDirectoryFiles(
            string directoryPath,
            bool recursive,
            FormatterRegistry registry,
            HashSet<string> results,
            List<string> errors)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly))
                {
                    if (registry.TryGetFormatter(file, out _))
                    {
                        results.Add(Path.GetFullPath(file));
                    }
                }

                if (!recursive)
                {
                    return;
                }

                foreach (var childDirectory in Directory.EnumerateDirectories(
                    directoryPath,
                    "*",
                    SearchOption.TopDirectoryOnly))
                {
                    if (IgnoredRecursiveDirectories.Contains(Path.GetFileName(childDirectory)))
                    {
                        continue;
                    }

                    if ((File.GetAttributes(childDirectory) & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    CollectDirectoryFiles(childDirectory, true, registry, results, errors);
                }
            }
            catch (Exception ex) when (IsPathException(ex))
            {
                errors.Add($"Unable to access path '{directoryPath}': {ex.Message}");
            }
        }

        private static bool IsPathException(Exception exception)
        {
            return exception is ArgumentException
                || exception is IOException
                || exception is NotSupportedException
                || exception is UnauthorizedAccessException;
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
