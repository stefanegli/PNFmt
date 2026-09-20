using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PNFmt.Benchmarks
{
    internal static class PerformanceRunner
    {
        public static int Run(string family, string reportPath)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
            var directory = Path.Combine(temporaryRoot, "PNFmtPerf-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var results = new List<PerformanceCaseResult>();
                if (family == "repository")
                {
                    Program.RunSorting(Path.Combine(directory, "sorting"), quick: false, samples: 7, results);
                    foreach (var scenario in new[] { "small-projects", "mixed-flat", "mixed-nested" })
                    {
                        var fixture = new RepositoryFixture(Path.Combine(directory, scenario), 128,
                            mixed: scenario != "small-projects", nested: scenario == "mixed-nested");
                        Program.RunRepository(scenario, fixture, 7, Math.Max(2, Math.Min(Environment.ProcessorCount, 8)), results);
                    }
                }
                else
                {
                    results.AddRange(FormatterPerformance.Run(family, directory));
                }

                var report = new
                {
                    SuiteVersion = 1,
                    Family = family,
                    Runtime = RuntimeInformation.FrameworkDescription,
                    OS = RuntimeInformation.OSDescription,
                    Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    ProcessorCount = Environment.ProcessorCount,
                    TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
                    Cases = results,
                };
                File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"Measured {results.Count} {family} cases: {reportPath}");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
            finally
            {
                if (Path.GetDirectoryName(directory) != temporaryRoot.TrimEnd(Path.DirectorySeparatorChar))
                {
                    throw new InvalidOperationException("Performance fixture cleanup must stay inside its temporary root.");
                }
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    internal sealed class PerformanceCaseResult
    {
        public string Id { get; set; }
        public bool Write { get; set; }
        public int Iterations { get; set; }
        public double[] Milliseconds { get; set; }
        public double[] AllocatedBytes { get; set; }
        public string OutputHash { get; set; }
    }
}
