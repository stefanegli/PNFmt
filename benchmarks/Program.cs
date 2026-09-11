using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PNFmt.Benchmarks
{
    internal static class Program
    {
        public static void Main()
        {
            var temporaryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PNFmtBenchmarks"));
            var directory = Path.GetFullPath(Path.Combine(temporaryRoot, Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(Path.Combine(directory, ".editorconfig"),
                    "root = true\n[*.csproj]\npnfmt_sort_entries = true\n");
                Console.WriteLine("Scenario       Entries       ms    Allocated MiB (best of 3 warm runs)");
                foreach (var metadata in new[] { false, true })
                {
                    foreach (var count in new[] { 500, 1000, 2000, 4000 })
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
                        formatter.Format(request);
                        var bestTime = double.MaxValue;
                        var bestAllocation = long.MaxValue;
                        for (var sample = 0; sample < 3; sample++)
                        {
                            var allocated = GC.GetAllocatedBytesForCurrentThread();
                            var watch = Stopwatch.StartNew();
                            formatter.Format(request);
                            watch.Stop();
                            bestAllocation = Math.Min(bestAllocation, GC.GetAllocatedBytesForCurrentThread() - allocated);
                            bestTime = Math.Min(bestTime, watch.Elapsed.TotalMilliseconds);
                        }

                        Console.WriteLine($"{(metadata ? "Metadata" : "Properties"),-14} {count,7} {bestTime,8:F2} {bestAllocation / 1048576.0,16:F2}");
                    }
                }
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

        private sealed class SilentLog : IFormatterLog
        {
            public void Write(Exception exception) { }
            public void WriteLine(string message) { }
        }
    }
}
