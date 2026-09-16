// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PNFmt.Cli;
using Xunit;

namespace PNFmt.Tests
{
    public sealed class FormattingReporterTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Message_meaning_controls_visibility_independently_of_wording(bool verbose)
        {
            using var directory = new TestDirectory();
            var file = directory.GetPath("Sample.test");
            var formatter = new ReportingFormatter();
            var run = new FormattingRunner(new FormatterRegistry(new[] { formatter }))
                .Run(new[] { file }, false, true, 1);
            var report = Report(directory.Path, run, verbose ? new[] { "--lint", "--verbose" } : new[] { "--lint" });

            Assert.Equal(0, report.ExitCode);
            Assert.Contains("warning CUSTOM001: A compatibility notice.", report.Error);
            Assert.DoesNotContain("ordinary detail", report.Error);
            Assert.DoesNotContain("progress with arbitrary wording", report.Output);
            Assert.DoesNotContain("compatibility notice", report.Output);
            Assert.Equal(verbose, report.Output.Contains("Updating indexes is ordinary detail"));
            Assert.Equal(verbose, report.Output.Contains("ordinary detail: warning PNFMT999: quoted text"));
            Assert.Equal(verbose, report.Output.Contains("[unchanged]"));
            Assert.Contains("diagnostics 0.", report.Output);
        }

        [Theory]
        [InlineData("--all", false, 0)]
        [InlineData("--check", false, 0)]
        [InlineData("--lint", false, 0)]
        [InlineData("--all", true, 0)]
        [InlineData("--check", true, 0)]
        [InlineData("--lint", true, 1)]
        public void Only_formatter_diagnostics_affect_lint_exit_status(string mode, bool diagnostics, int expectedExit)
        {
            var root = Path.GetTempPath();
            var file = Path.Combine(root, "Sample.csproj");
            var findings = diagnostics ? new[] { new FormatterDiagnostic("CSPROJ001", "A project issue.", 7) } : Array.Empty<FormatterDiagnostic>();
            var outcome = new FileFormattingOutcome(file, new FileFormatResult(FileFormatStatus.Unchanged, findings), null,
                new[] { FormatterLogMessage.Warning(file, "PNFMT004", "A compatibility notice.") }, Array.Empty<Exception>());
            var report = Report(root, new FormattingRunResult(new[] { outcome }, TimeSpan.FromMilliseconds(125)), new[] { mode });

            Assert.Equal(expectedExit, report.ExitCode);
            Assert.Contains("warning PNFMT004", report.Error);
            Assert.Equal(diagnostics, report.Output.Contains("Sample.csproj(7): warning CSPROJ001: A project issue."));
            Assert.Contains("in 0.125s.", report.Output);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Errors_take_precedence_over_formatting_changes(bool targetError)
        {
            var root = Path.GetTempPath();
            var updated = new FileFormattingOutcome(Path.Combine(root, "Changed.cs"), new FileFormatResult(FileFormatStatus.Updated), null,
                Array.Empty<FormatterLogMessage>(), Array.Empty<Exception>());
            var failed = new FileFormattingOutcome(Path.Combine(root, "Failed.cs"), null, new IOException("Cannot read input."),
                Array.Empty<FormatterLogMessage>(), Array.Empty<Exception>());
            var outcomes = targetError ? new[] { updated } : new[] { updated, failed };
            var errors = targetError ? new[] { "Path not found: Missing.cs" } : Array.Empty<string>();
            var report = Report(root, new FormattingRunResult(outcomes, TimeSpan.Zero), new[] { "--check" }, errors);

            Assert.Equal(2, report.ExitCode);
            Assert.Contains("Would update 1", report.Output);
            Assert.Contains(targetError ? "Path not found: Missing.cs" : "Cannot read input.", report.Error);
        }

        private static (int ExitCode, string Output, string Error) Report(
            string workingDirectory, FormattingRunResult run, string[] arguments, string[] errors = null)
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            var targets = new TargetFileResolution(run.Outcomes.Select(outcome => outcome.File).ToArray(),
                errors ?? Array.Empty<string>(), gitFiltered: false, maxCpuCount: 1);
            var exitCode = new FormattingReporter(output, error, workingDirectory, "pnfmt test")
                .Report(targets, run, CommandLineOptions.Parse(arguments));
            return (exitCode, output.ToString(), error.ToString());
        }

        private sealed class ReportingFormatter : IFileFormatter
        {
            public IReadOnlyCollection<string> FileExtensions { get; } = new[] { ".test" };
            public string Name => "reporting";

            public FileFormatResult Format(FileFormatRequest request)
            {
                request.Log.Warning(request.FilePath, "CUSTOM001", "A compatibility notice.");
                request.Log.Progress("progress with arbitrary wording");
                request.Log.WriteLine("Updating indexes is ordinary detail");
                request.Log.WriteLine("ordinary detail: warning PNFMT999: quoted text");
                return new FileFormatResult(FileFormatStatus.Unchanged);
            }
        }
    }
}
