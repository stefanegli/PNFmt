using System;
using System.Diagnostics;

namespace PNFmt.Benchmarks
{
    internal static class BenchmarkMeasurement
    {
        public static void Run<T>(string scenario, string phase, int items, int workers, int samples,
            Func<T> action, Action<T> validate, Action prepare = null)
        {
            prepare?.Invoke();
            validate(action());
            var elapsed = new double[samples];
            var allocated = new long[samples];
            var watch = new Stopwatch();
            for (var index = 0; index < samples; index++)
            {
                prepare?.Invoke();
                var before = GC.GetTotalAllocatedBytes(precise: true);
                watch.Restart();
                var result = action();
                watch.Stop();
                allocated[index] = GC.GetTotalAllocatedBytes(precise: true) - before;
                elapsed[index] = watch.Elapsed.TotalMilliseconds;
                validate(result);
            }

            Array.Sort(elapsed);
            Array.Sort(allocated);
            Console.WriteLine($"{scenario,-18} {phase,-10} {items,6} {workers,7} {elapsed[0],10:F2} {elapsed[samples / 2],10:F2} {allocated[samples / 2] / 1048576.0,11:F2}");
        }
    }
}
