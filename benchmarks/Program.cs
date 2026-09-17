using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using PNFmt.Cli;

namespace PNFmt.Benchmarks
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length > 1 || (args.Length == 1 && args[0] != "--quick"))
            {
                Console.Error.WriteLine("Usage: dotnet run --project benchmarks -c Release -- [--quick]");
                return 2;
            }

            var quick = args.Length == 1;
            var samples = quick ? 1 : 3;
            var fileCount = quick ? 64 : 2048;
            var workers = Math.Max(2, Math.Min(Environment.ProcessorCount, 8));
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var temporaryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PNFmtBenchmarks"));
            var directory = Path.GetFullPath(Path.Combine(temporaryRoot, Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            try
            {
                Console.WriteLine($".NET {Environment.Version}; {RuntimeInformation.OSDescription}; {RuntimeInformation.ProcessArchitecture}; {Environment.ProcessorCount} logical CPUs");
                Console.WriteLine($"{samples} measured warm runs after one warm-up; {fileCount} files per repository; parallel workers: {workers}");
                Console.WriteLine("Setup, resets, and correctness checks are excluded. Allocations include all managed threads.");
                Console.WriteLine($"{"Scenario",-18} {"Phase",-10} {"Items",6} {"Workers",7} {"Min ms",10} {"Median ms",10} {"Median MiB",11}");
                RunSorting(Path.Combine(directory, "sorting"), quick, samples);
                foreach (var scenario in new[] { "small-projects", "mixed-flat", "mixed-nested" })
                {
                    var fixture = new RepositoryFixture(Path.Combine(directory, scenario), fileCount,
                        mixed: scenario != "small-projects", nested: scenario == "mixed-nested");
                    RunRepository(scenario, fixture, samples, workers);
                }

                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
            finally
            {
                if (Path.GetDirectoryName(directory) != temporaryRoot)
                {
                    throw new InvalidOperationException("Benchmark cleanup must stay inside its temporary root.");
                }

                Directory.Delete(directory, recursive: true);
            }
        }

        private static void RunRepository(string scenario, RepositoryFixture fixture, int samples, int parallelism)
        {
            var registry = FormatterCatalog.CreateDefault();
            var resolver = new TargetFileResolver(registry, registry,
                new FilePatternMatcher(RepositoryFixture.Extensions.Select(extension => "*." + extension)));
            TargetFileResolution Discover() => resolver.Resolve(new[] { fixture.DirectoryPath }, recursive: true, allFiles: true);
            BenchmarkMeasurement.Run(scenario, "discovery", fixture.Files.Count, 1, samples, Discover, fixture.ValidateDiscovery);
            var files = Discover().Files;
            var runner = new FormattingRunner(registry);
            foreach (var workers in new[] { 1, parallelism })
            {
                fixture.Restore();
                BenchmarkMeasurement.Run(scenario, "preview", files.Count, workers, samples,
                    () => runner.Run(files, writeChanges: false, lint: false, workers),
                    result => fixture.Validate(result, FileFormatStatus.Updated, formatted: false));
                BenchmarkMeasurement.Run(scenario, "write", files.Count, workers, samples,
                    () => runner.Run(files, writeChanges: true, lint: false, workers),
                    result => fixture.Validate(result, FileFormatStatus.Updated, formatted: true), fixture.Restore);
                BenchmarkMeasurement.Run(scenario, "unchanged", files.Count, workers, samples,
                    () => runner.Run(files, writeChanges: false, lint: false, workers),
                    result => fixture.Validate(result, FileFormatStatus.Unchanged, formatted: true));
            }
        }

        private static void RunSorting(string directory, bool quick, int samples)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, ".editorconfig"),
                "root = true\n[*.csproj]\npnfmt_enabled = true\npnfmt_formatter = csproj\npnfmt_sort_entries = true\n");
            foreach (var metadata in new[] { false, true })
            {
                foreach (var count in quick ? new[] { 500 } : new[] { 500, 1000, 2000, 4000 })
                {
                    var name = metadata ? "Tag" : "DefineConstants";
                    var entries = string.Concat(Enumerable.Range(0, count).Select(index =>
                        "<" + name + ">" + (metadata ? "%(" : "$(") + name + ");A" + index + "</" + name + ">"));
                    var body = metadata ? "<ItemGroup><None Include='file'>" + entries + "</None></ItemGroup>"
                        : "<PropertyGroup>" + entries + "</PropertyGroup>";
                    var path = Path.Combine(directory, "Repeated.csproj");
                    File.WriteAllText(path, "<Project Sdk='Microsoft.NET.Sdk'>" + body + "</Project>");
                    var formatter = new CsProjFormatter();
                    var request = new FileFormatRequest(path, false, false, new SilentLog());
                    BenchmarkMeasurement.Run(metadata ? "metadata" : "properties", "preview", count, 1, samples,
                        () => formatter.Format(request), result =>
                        {
                            if (result.Status != FileFormatStatus.Updated || result.Diagnostics.Count != 0)
                            {
                                throw new InvalidOperationException("Sorting benchmark did not format its input.");
                            }
                        });
                }
            }
        }

        private sealed class SilentLog : IFormatterLog
        {
            public void Write(Exception exception) => throw new InvalidOperationException("Unexpected formatter error.", exception);
            public void WriteLine(string message) { }
        }
    }
}
