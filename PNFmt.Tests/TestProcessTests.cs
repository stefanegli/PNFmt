using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Xunit;

namespace PNFmt.Tests
{
    public sealed class TestProcessTests
    {
        [Fact]
        public async Task A_process_waiting_for_input_is_killed_on_timeout()
        {
            var directory = Path.Combine(Path.GetTempPath(), "PNFmtProcessTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(Path.Combine(directory, ".editorconfig"),
                    "root = true\n[*.csproj]\ncsproj_formatter_sort_entries = true\n");
                var start = new ProcessStartInfo("dotnet")
                {
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = directory,
                };
                foreach (var argument in new[] { typeof(Cli.Program).Assembly.Location, "--write-default-config", directory })
                {
                    start.ArgumentList.Add(argument);
                }

                using (var process = Process.Start(start))
                {
                    try
                    {
                        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
                            TestProcess.ReadAsync(process, TimeSpan.FromMilliseconds(500)).WaitAsync(TimeSpan.FromSeconds(5)));
                        Assert.Contains("deadline", exception.Message);
                        Assert.True(process.HasExited);
                    }
                    finally
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                            await process.WaitForExitAsync();
                        }
                    }
                }
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
