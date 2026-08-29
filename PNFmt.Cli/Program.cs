// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
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
            var paths = new List<string>();

            foreach (var arg in args ?? Array.Empty<string>())
            {
                if (!stopOptions && string.Equals(arg, "--", StringComparison.Ordinal))
                {
                    stopOptions = true;
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

            var log = new ConsoleLog(verbose);
            var workingDirectory = Environment.CurrentDirectory;
            var changed = 0;
            var unchanged = 0;
            var skipped = 0;
            var failed = 0;
            var diagnosticCount = 0;

            foreach (var file in files)
            {
                try
                {
                    if (!registry.TryGetFormatter(file, out var formatter))
                    {
                        throw new InvalidOperationException(
                            $"No formatter is registered for '{Path.GetExtension(file)}'.");
                    }

                    var request = new FileFormatRequest(file, !dryRun, lint, log);
                    var result = formatter.Format(request);
                    if (result is null)
                    {
                        throw new InvalidOperationException(
                            $"Formatter '{formatter.Name}' returned no result.");
                    }

                    switch (result.Status)
                    {
                        case FileFormatStatus.Updated:
                            changed++;
                            WriteStatus(dryRun ? "would-update" : "updated", file, workingDirectory);
                            break;

                        case FileFormatStatus.Unchanged:
                            unchanged++;
                            WriteStatus("unchanged", file, workingDirectory);
                            break;

                        case FileFormatStatus.Skipped:
                            skipped++;
                            WriteStatus("skipped", file, workingDirectory);
                            break;

                        default:
                            throw new InvalidOperationException(
                                $"Formatter '{formatter.Name}' returned an unknown status.");
                    }

                    foreach (var diagnostic in result.Diagnostics)
                    {
                        diagnosticCount++;
                        WriteDiagnostic(diagnostic, file, workingDirectory);
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    WriteStatus("failed", file, workingDirectory);
                    Console.Error.WriteLine($"Failed to format {file}: {ex.Message}");
                    if (verbose)
                    {
                        Console.Error.WriteLine(ex);
                    }
                }
            }

            if (!verbose)
            {
                var changeLabel = dryRun ? "Would update" : "Updated";
                Console.WriteLine(
                    $"Processed {files.Count} file(s). {changeLabel} {changed}, "
                    + $"unchanged {unchanged}, skipped {skipped}, failed {failed}"
                    + (lint ? $", diagnostics {diagnosticCount}." : "."));
            }

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
            writer.WriteLine("  PNFmt settings use the pnfmt_ prefix; legacy formatter settings remain fallbacks.");
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

        private sealed class ConsoleLog : IFormatterLog
        {
            private readonly bool verbose;

            public ConsoleLog(bool verbose)
            {
                this.verbose = verbose;
            }

            public void Write(Exception exception)
            {
                if (this.verbose)
                {
                    Console.Error.WriteLine(exception);
                }
            }

            public void WriteLine(string message)
            {
                if (message.IndexOf(": warning PNFMT", StringComparison.Ordinal) >= 0)
                {
                    Console.Error.WriteLine(message);
                    return;
                }

                if (!this.verbose)
                {
                    return;
                }

                if (message.StartsWith("Updating ", StringComparison.OrdinalIgnoreCase)
                    || message.StartsWith("Would update ", StringComparison.OrdinalIgnoreCase)
                    || message.StartsWith("Skipping ", StringComparison.OrdinalIgnoreCase)
                    || message.StartsWith("Update was not required", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                Console.WriteLine(message);
            }
        }
    }
}
