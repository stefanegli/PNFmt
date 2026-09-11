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
            using (var stagedInput = TemporarySnapshotDirectory.CopyFrom(inputRoot))
            {
                var stagedFile = stagedInput.GetPath(relativePath);
                var result = await RunCliAsync(stagedInput.Path, stagedFile);
                var actual = File.ReadAllText(stagedFile);
                var context = $"Case: {caseName}{Environment.NewLine}"
                    + $"stdout:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}"
                    + $"stderr:{Environment.NewLine}{result.StandardError}";
                var changed = !File.ReadAllBytes(inputFile).SequenceEqual(File.ReadAllBytes(stagedFile));

                Assert.True(result.ExitCode == 0, context);
                Assert.Contains(Path.GetFileName(relativePath), result.StandardOutput);
                Assert.Contains(changed ? "[updated]" : "[unchanged]", result.StandardOutput);
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
