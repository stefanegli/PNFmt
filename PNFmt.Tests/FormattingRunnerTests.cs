// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;

using PNFmt.Cli;

using Xunit;

namespace PNFmt.Tests
{
    public sealed class FormattingRunnerTests
    {
        [Fact]
        public void Runner_finishes_editorconfig_files_before_dependent_files()
        {
            using var directory = new TestDirectory();
            var configuration = directory.Write(".editorconfig", "root = true\n");
            var state = new ConfigurationState();
            var registry = new FormatterRegistry(
                new IFileFormatter[]
                {
                    new ConfigurationFormatter(state),
                    new DependentFormatter(state),
                });
            var files = new[] { directory.GetPath("project.dependent"), configuration };

            var result = new FormattingRunner(registry).Run(files, true, false, 2);

            Assert.Equal(files[0], result.Outcomes[0].File);
            Assert.Equal(files[1], result.Outcomes[1].File);
            Assert.All(result.Outcomes, outcome => Assert.Null(outcome.Error));
        }

        [Fact]
        public void Runner_formats_parent_editorconfig_before_nested_editorconfig()
        {
            using var directory = new TestDirectory();
            var configuration = directory.Write(".editorconfig", "root = true\n");
            var state = new ConfigurationState();
            var registry = new FormatterRegistry(
                new IFileFormatter[] { new ConfigurationFormatter(state) });
            var files = new[]
            {
                directory.Write("nested/.editorconfig", ""),
                configuration,
            };

            var result = new FormattingRunner(registry).Run(files, true, false, 2);

            Assert.Equal(files[0], result.Outcomes[0].File);
            Assert.Equal(files[1], result.Outcomes[1].File);
            Assert.All(result.Outcomes, outcome => Assert.Null(outcome.Error));
        }

        [Fact]
        public void Runner_processes_files_concurrently_and_keeps_result_order()
        {
            using (var formatter = new ConcurrencyFormatter())
            {
                var registry = new FormatterRegistry(new[] { formatter });
                var files = new[] { "first.concurrent", "second.concurrent" };

                var result = new FormattingRunner(registry).Run(files, true, false, 2);

                Assert.Equal(2, formatter.PeakConcurrency);
                Assert.Equal(files[0], result.Outcomes[0].File);
                Assert.Equal(files[1], result.Outcomes[1].File);
                Assert.All(result.Outcomes, outcome => Assert.Null(outcome.Error));
            }
        }

        [Fact]
        public void Runner_rejects_non_positive_parallelism()
        {
            var registry = new FormatterRegistry(new[] { new NoOpFormatter() });
            var runner = new FormattingRunner(registry);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => runner.Run(Array.Empty<string>(), true, false, 0));
        }

        private sealed class ConcurrencyFormatter : IFileFormatter, IDisposable
        {
            private readonly Barrier barrier = new Barrier(2);
            private int active;
            private int peakConcurrency;

            public IReadOnlyCollection<string> FileExtensions { get; } =
                Array.AsReadOnly(new[] { ".concurrent" });

            public string Name => "concurrency";

            public int PeakConcurrency => this.peakConcurrency;

            public void Dispose()
            {
                this.barrier.Dispose();
            }

            public FileFormatResult Format(FileFormatRequest request)
            {
                var current = Interlocked.Increment(ref this.active);
                SetMaximum(ref this.peakConcurrency, current);
                try
                {
                    if (!this.barrier.SignalAndWait(TimeSpan.FromSeconds(5)))
                    {
                        throw new TimeoutException("The formatting calls did not overlap.");
                    }

                    return new FileFormatResult(FileFormatStatus.Unchanged);
                }
                finally
                {
                    Interlocked.Decrement(ref this.active);
                }
            }

            private static void SetMaximum(ref int target, int value)
            {
                var current = Volatile.Read(ref target);
                while (current < value)
                {
                    var observed = Interlocked.CompareExchange(ref target, value, current);
                    if (observed == current)
                    {
                        return;
                    }

                    current = observed;
                }
            }
        }

        private sealed class ConfigurationFormatter : IFileFormatter
        {
            private readonly ConfigurationState state;

            public ConfigurationFormatter(ConfigurationState state)
            {
                this.state = state;
            }

            public IReadOnlyCollection<string> FileExtensions { get; } =
                Array.AsReadOnly(new[] { ".editorconfig" });

            public string Name => "configuration";

            public FileFormatResult Format(FileFormatRequest request)
            {
                var isNested = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(request.FilePath)) == "nested";
                if (isNested && !this.state.IsReady)
                {
                    throw new InvalidOperationException(
                        "The nested EditorConfig file was formatted before its parent.");
                }

                this.state.IsReady = true;
                return new FileFormatResult(FileFormatStatus.Unchanged);
            }
        }

        private sealed class ConfigurationState
        {
            public bool IsReady { get; set; }
        }

        private sealed class DependentFormatter : IFileFormatter
        {
            private readonly ConfigurationState state;

            public DependentFormatter(ConfigurationState state)
            {
                this.state = state;
            }

            public IReadOnlyCollection<string> FileExtensions { get; } =
                Array.AsReadOnly(new[] { ".dependent" });

            public string Name => "dependent";

            public FileFormatResult Format(FileFormatRequest request)
            {
                if (!this.state.IsReady)
                {
                    throw new InvalidOperationException(
                        "The EditorConfig dependency was not processed first.");
                }

                return new FileFormatResult(FileFormatStatus.Unchanged);
            }
        }

        private sealed class NoOpFormatter : IFileFormatter
        {
            public IReadOnlyCollection<string> FileExtensions { get; } =
                Array.AsReadOnly(new[] { ".noop" });

            public string Name => "noop";

            public FileFormatResult Format(FileFormatRequest request)
            {
                return new FileFormatResult(FileFormatStatus.Unchanged);
            }
        }
    }
}
