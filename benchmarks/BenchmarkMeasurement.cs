using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PNFmt.Benchmarks
{
    internal static class BenchmarkMeasurement
    {
        public static void Run<T>(string scenario, string phase, int items, int workers, int samples,
            Func<T> action, Action<T> validate, Action prepare = null, ICollection<PerformanceCaseResult> results = null)
        {
            var watch = new Stopwatch();
            var warmMilliseconds = 0.0;
            var warmCount = 0;
            do
            {
                prepare?.Invoke();
                watch.Restart();
                var result = action();
                watch.Stop();
                warmMilliseconds += watch.Elapsed.TotalMilliseconds;
                warmCount++;
                validate(result);
            }
            while (results is not null && (warmCount < 3 || warmMilliseconds < 300));

            // Batch read-only operations so short repository cases are not
            // dominated by a scheduler interruption. Writes need an untimed
            // reset before every operation, so each write sample stays single.
            var iterations = results is null || prepare is not null ? 1
                : Math.Clamp((int)(100.0 / (warmMilliseconds / warmCount)), 1, 100);
            var elapsed = new double[samples];
            var allocated = new double[samples];
            var batch = new T[iterations];
            for (var index = 0; index < samples; index++)
            {
                prepare?.Invoke();
                var before = GC.GetTotalAllocatedBytes(precise: true);
                watch.Restart();
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    batch[iteration] = action();
                }
                watch.Stop();
                allocated[index] = (GC.GetTotalAllocatedBytes(precise: true) - before) / (double)iterations;
                elapsed[index] = watch.Elapsed.TotalMilliseconds / iterations;
                foreach (var result in batch)
                {
                    validate(result);
                }
            }

            Array.Sort(elapsed);
            Array.Sort(allocated);
            results?.Add(new PerformanceCaseResult
            {
                Id = $"repository/{scenario}/{phase}/{items}/{workers}",
                Write = phase == "write",
                Milliseconds = elapsed,
                AllocatedBytes = allocated,
                Iterations = iterations,
            });
            Console.WriteLine($"{scenario,-18} {phase,-10} {items,6} {workers,7} {elapsed[0],10:F2} {elapsed[samples / 2],10:F2} {allocated[samples / 2] / 1048576.0,11:F2}");
        }
    }
}
