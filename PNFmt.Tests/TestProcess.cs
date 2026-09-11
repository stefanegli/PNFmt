using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PNFmt.Tests
{
    internal static class TestProcess
    {
        public static async Task<(int ExitCode, string StandardOutput, string StandardError)> ReadAsync(
            Process process, TimeSpan? timeout = null)
        {
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            var deadline = timeout ?? TimeSpan.FromSeconds(30);
            try
            {
                await Task.WhenAll(process.WaitForExitAsync(), output, error).WaitAsync(deadline);
                return (process.ExitCode, await output, await error);
            }
            catch (TimeoutException exception)
            {
                throw new TimeoutException($"Test process exceeded its {deadline.TotalSeconds:g} second deadline.", exception);
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
            }
        }
    }
}
