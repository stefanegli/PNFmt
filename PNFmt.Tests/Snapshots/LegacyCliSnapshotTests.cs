// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PNFmt.Tests.Formatter.CsProj.TestFoundation;
using Xunit;

namespace PNFmt.Tests.Snapshots
{
    public sealed class LegacyCliSnapshotTests
    {
        public static IEnumerable<object[]> CsProjSnapshots =>
            FormattingCaseSource.Create(GetCsProjFixtureRoot())
                .Select(testCase => new object[]
                {
                    testCase.RelativePath,
                    testCase.InputFile,
                    testCase.ExpectedFile,
                    testCase.CaseName,
                });

        [Theory]
        [MemberData(nameof(CsProjSnapshots))]
        public async Task Unified_cli_matches_every_legacy_csproj_snapshot(
            string relativePath,
            string inputFile,
            string expectedFile,
            string caseName)
        {
            var inputRoot = Path.Combine(GetCsProjFixtureRoot(), "input");
            using (var stagedInput = TemporarySnapshotDirectory.CopyFrom(inputRoot))
            {
                var stagedFile = stagedInput.GetPath(relativePath);
                var result = await RunCliAsync(stagedInput.Path, stagedFile);
                var context = $"Case: {caseName}{Environment.NewLine}"
                    + $"stdout:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}"
                    + $"stderr:{Environment.NewLine}{result.StandardError}";

                Assert.True(result.ExitCode == 0, context);
                Assert.Contains(Path.GetFileName(relativePath), result.StandardOutput);
                Assert.Equal(File.ReadAllText(expectedFile), File.ReadAllText(stagedFile));

                if (!string.Equals(
                    File.ReadAllText(inputFile),
                    File.ReadAllText(expectedFile),
                    StringComparison.Ordinal))
                {
                    Assert.Contains("[updated]", result.StandardOutput);
                }
            }
        }

        private static string GetCsProjFixtureRoot()
        {
            return Path.Combine(
                AppContext.BaseDirectory,
                "Formatter",
                "CsProj",
                "_files");
        }

        private static async Task<CliRunResult> RunCliAsync(string workingDirectory, string targetFile)
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
            startInfo.ArgumentList.Add(targetFile);

            using (var process = Process.Start(startInfo))
            {
                if (process is null)
                {
                    throw new InvalidOperationException("Failed to start the PNFmt CLI process.");
                }

                var standardOutputTask = process.StandardOutput.ReadToEndAsync();
                var standardErrorTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                return new CliRunResult(
                    process.ExitCode,
                    await standardOutputTask,
                    await standardErrorTask);
            }
        }

        private sealed class CliRunResult
        {
            public CliRunResult(int exitCode, string standardOutput, string standardError)
            {
                this.ExitCode = exitCode;
                this.StandardOutput = standardOutput;
                this.StandardError = standardError;
            }

            public int ExitCode { get; }

            public string StandardError { get; }

            public string StandardOutput { get; }
        }
    }
}
