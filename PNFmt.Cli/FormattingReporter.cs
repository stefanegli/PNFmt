// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Globalization;
using System.IO;
using System.Linq;

using PNFmt;

namespace PNFmt.Cli
{
    internal sealed class FormattingReporter
    {
        private static readonly int StatusColumnWidth =
            new[] { "updated", "unchanged", "skipped", "would-update", "failed" }.Max(status => status.Length) + 2;

        private readonly TextWriter output;
        private readonly TextWriter error;
        private readonly string workingDirectory;
        private readonly string versionLabel;

        public FormattingReporter(TextWriter output, TextWriter error, string workingDirectory, string versionLabel)
        {
            this.output = output ?? throw new ArgumentNullException(nameof(output));
            this.error = error ?? throw new ArgumentNullException(nameof(error));
            this.workingDirectory = workingDirectory;
            this.versionLabel = versionLabel;
        }

        public int Report(TargetFileResolution targets, FormattingRunResult run, CommandLineOptions options)
        {
            foreach (var error in targets.Errors)
            {
                this.error.WriteLine(error);
            }

            if (run.Outcomes.Count == 0)
            {
                this.output.WriteLine(targets.GitFiltered
                    ? "No changed supported files found."
                    : "No supported files found.");
                return targets.Errors.Count > 0 ? 2 : 0;
            }

            var changed = 0;
            var unchanged = 0;
            var skipped = 0;
            var failed = 0;
            var diagnosticCount = 0;
            foreach (var outcome in run.Outcomes)
            {
                this.WriteLog(outcome, options.Verbose);
                if (outcome.Error is not null)
                {
                    failed++;
                    if (options.Verbose)
                    {
                        this.WriteStatus("failed", outcome.File);
                    }

                    this.error.WriteLine($"Failed to format {outcome.File}: {outcome.Error.Message}");
                    if (options.Verbose)
                    {
                        this.error.WriteLine(outcome.Error);
                    }

                    continue;
                }

                var status = outcome.Result.Status;
                switch (status)
                {
                    case FileFormatStatus.Updated: changed++; break;
                    case FileFormatStatus.Unchanged: unchanged++; break;
                    case FileFormatStatus.Skipped: skipped++; break;
                }

                if (options.Verbose)
                {
                    var statusLabel = status == FileFormatStatus.Updated && options.DryRun
                        ? "would-update" : status.ToString().ToLowerInvariant();
                    this.WriteStatus(statusLabel, outcome.File);
                }

                foreach (var diagnostic in outcome.Result.Diagnostics)
                {
                    diagnosticCount++;
                    var displayPath = this.DisplayPath(outcome.File);
                    var location = diagnostic.LineNumber.HasValue
                        ? $"{displayPath}({diagnostic.LineNumber.Value})" : displayPath;
                    this.output.WriteLine($"{location}: warning {diagnostic.Code}: {diagnostic.Message}");
                }
            }

            var changeLabel = options.DryRun ? "Would update" : "Updated";
            var elapsed = run.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture);
            this.output.WriteLine(
                $"{this.versionLabel}: Processed {run.Outcomes.Count} file(s) in {elapsed}s. {changeLabel} {changed}, "
                + $"unchanged {unchanged}, skipped {skipped}, failed {failed}"
                + (options.Lint ? $", diagnostics {diagnosticCount}." : "."));

            if (failed > 0 || targets.Errors.Count > 0)
            {
                return 2;
            }

            // Compatibility warnings are log messages, not formatter diagnostics.
            // They remain visible without turning a successful check into a failure.
            return options.Check && (changed > 0 || (options.Lint && diagnosticCount > 0)) ? 1 : 0;
        }

        private string DisplayPath(string file)
        {
            var relative = Path.GetRelativePath(this.workingDirectory, Path.GetFullPath(file));
            return string.IsNullOrEmpty(relative) ? "." : relative;
        }

        private void WriteLog(FileFormattingOutcome outcome, bool verbose)
        {
            foreach (var message in outcome.LogMessages)
            {
                if (message.Kind == FormatterLogMessageKind.Warning)
                {
                    this.error.WriteLine(message.ToString());
                }
                else if (verbose && message.Kind == FormatterLogMessageKind.Detail)
                {
                    this.output.WriteLine(message.Message);
                }
            }

            if (verbose)
            {
                foreach (var exception in outcome.LoggedExceptions)
                {
                    this.error.WriteLine(exception);
                }
            }
        }

        private void WriteStatus(string status, string file)
        {
            var label = $"[{status}]".PadRight(StatusColumnWidth);
            this.output.WriteLine($"{label} {this.DisplayPath(file)}");
        }
    }
}
