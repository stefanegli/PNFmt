// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

namespace PNFmt.Tests.Snapshots
{
    internal static class CliTestRunner
    {
        public static async Task<string> RunAndAssertAsync(
            string fixtureRoot,
            string relativePath,
            string inputFile,
            string caseName)
        {
            var inputRoot = Path.Combine(fixtureRoot, "input");
            using (var stagedInput = TestDirectory.CopyFrom(inputRoot))
            {
                var stagedFile = stagedInput.GetPath(relativePath);
                var inputBytes = File.ReadAllBytes(stagedFile);
                var preview = await RunCliAsync(stagedInput.Path, "--check", stagedFile);
                Assert.Equal(inputBytes, File.ReadAllBytes(stagedFile));
                var result = await RunCliAsync(stagedInput.Path, stagedFile);
                var actual = File.ReadAllText(stagedFile);
                var context = $"Case: {caseName}{Environment.NewLine}"
                    + $"stdout:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}"
                    + $"stderr:{Environment.NewLine}{result.StandardError}";
                var formattedBytes = File.ReadAllBytes(stagedFile);
                var changed = !File.ReadAllBytes(inputFile).SequenceEqual(formattedBytes);

                Assert.Equal(changed ? 1 : 0, preview.ExitCode);
                Assert.True(result.ExitCode == 0, context);
                Assert.Contains(Path.GetFileName(relativePath), result.StandardOutput);
                Assert.Contains(changed ? "[updated]" : "[unchanged]", result.StandardOutput);
                var check = await RunCliAsync(stagedInput.Path, "--check", stagedFile);
                Assert.True(check.ExitCode == 0, check.StandardOutput + check.StandardError);
                Assert.Equal(formattedBytes, File.ReadAllBytes(stagedFile));
                return actual;
            }
        }

        public static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunCliAsync(
            string workingDirectory, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory,
            };
            startInfo.ArgumentList.Add(typeof(PNFmt.Cli.Program).Assembly.Location);
            startInfo.ArgumentList.Add("--verbose");
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using (var process = Process.Start(startInfo))
            {
                if (process is null)
                {
                    throw new InvalidOperationException("Failed to start the PNFmt CLI process.");
                }

                return await TestProcess.ReadAsync(process);
            }
        }
    }
}
