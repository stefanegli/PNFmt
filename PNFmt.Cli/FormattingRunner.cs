// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PNFmt;

namespace PNFmt.Cli
{
    internal sealed class FormattingRunner
    {
        private readonly FormatterRegistry registry;

        public FormattingRunner(FormatterRegistry registry)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public FormattingRunResult Run(
            IReadOnlyList<string> files,
            bool writeChanges,
            bool lint,
            int threadCount)
        {
            if (files is null)
            {
                throw new ArgumentNullException(nameof(files));
            }

            if (threadCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(threadCount),
                    "The thread count must be greater than zero.");
            }

            var outcomes = new FileFormattingOutcome[files.Count];
            var stopwatch = Stopwatch.StartNew();
            var editorConfigIndexes = Enumerable.Range(0, files.Count)
                .Where(index => IsEditorConfig(files[index]))
                .ToArray();
            var otherIndexes = Enumerable.Range(0, files.Count)
                .Where(index => !IsEditorConfig(files[index]))
                .ToArray();
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = threadCount };
            this.FormatFiles(
                files,
                editorConfigIndexes,
                outcomes,
                writeChanges,
                lint,
                parallelOptions);
            this.FormatFiles(
                files,
                otherIndexes,
                outcomes,
                writeChanges,
                lint,
                parallelOptions);
            stopwatch.Stop();

            return new FormattingRunResult(outcomes, stopwatch.Elapsed);
        }

        private static bool IsEditorConfig(string file)
        {
            return string.Equals(
                Path.GetFileName(file),
                ".editorconfig",
                StringComparison.OrdinalIgnoreCase);
        }

        private void FormatFiles(
            IReadOnlyList<string> files,
            IReadOnlyList<int> indexes,
            FileFormattingOutcome[] outcomes,
            bool writeChanges,
            bool lint,
            ParallelOptions parallelOptions)
        {
            Parallel.ForEach(
                indexes,
                parallelOptions,
                index => outcomes[index] = this.FormatFile(files[index], writeChanges, lint));
        }

        private FileFormattingOutcome FormatFile(string file, bool writeChanges, bool lint)
        {
            var log = new BufferedFormatterLog();
            try
            {
                if (!this.registry.TryGetFormatter(file, out var formatter))
                {
                    throw new InvalidOperationException(
                        $"No formatter is registered for '{System.IO.Path.GetExtension(file)}'.");
                }

                var request = new FileFormatRequest(file, writeChanges, lint, log);
                var result = formatter.Format(request);
                if (result is null)
                {
                    throw new InvalidOperationException(
                        $"Formatter '{formatter.Name}' returned no result.");
                }

                if (result.Status != FileFormatStatus.Updated
                    && result.Status != FileFormatStatus.Unchanged
                    && result.Status != FileFormatStatus.Skipped)
                {
                    throw new InvalidOperationException(
                        $"Formatter '{formatter.Name}' returned an unknown status.");
                }

                return new FileFormattingOutcome(file, result, null, log.Messages, log.Exceptions);
            }
            catch (Exception exception)
            {
                return new FileFormattingOutcome(file, null, exception, log.Messages, log.Exceptions);
            }
        }

        private sealed class BufferedFormatterLog : IFormatterLog
        {
            private readonly List<Exception> exceptions = new List<Exception>();
            private readonly List<string> messages = new List<string>();

            public IReadOnlyList<Exception> Exceptions => this.exceptions;

            public IReadOnlyList<string> Messages => this.messages;

            public void Write(Exception exception)
            {
                if (exception is not null)
                {
                    this.exceptions.Add(exception);
                }
            }

            public void WriteLine(string message)
            {
                if (message is not null)
                {
                    this.messages.Add(message);
                }
            }
        }
    }

    internal sealed class FormattingRunResult
    {
        public FormattingRunResult(
            IReadOnlyList<FileFormattingOutcome> outcomes,
            TimeSpan elapsed)
        {
            this.Outcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));
            this.Elapsed = elapsed;
        }

        public TimeSpan Elapsed { get; }

        public IReadOnlyList<FileFormattingOutcome> Outcomes { get; }
    }

    internal sealed class FileFormattingOutcome
    {
        public FileFormattingOutcome(
            string file,
            FileFormatResult result,
            Exception error,
            IReadOnlyList<string> logMessages,
            IReadOnlyList<Exception> loggedExceptions)
        {
            this.File = file ?? throw new ArgumentNullException(nameof(file));
            this.Result = result;
            this.Error = error;
            this.LogMessages = logMessages ?? throw new ArgumentNullException(nameof(logMessages));
            this.LoggedExceptions = loggedExceptions
                ?? throw new ArgumentNullException(nameof(loggedExceptions));
        }

        public Exception Error { get; }

        public string File { get; }

        public IReadOnlyList<Exception> LoggedExceptions { get; }

        public IReadOnlyList<string> LogMessages { get; }

        public FileFormatResult Result { get; }
    }
}
