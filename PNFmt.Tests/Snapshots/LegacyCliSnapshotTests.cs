// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PNFmt.Tests.Snapshots
{
    public sealed class LegacyCliSnapshotTests
    {
        public static IEnumerable<object[]> CsProjSnapshots =>
            FileSnapshotCaseSource.Create(GetCsProjFixtureRoot(), ".csproj")
                .Select(testCase => new object[]
                {
                    testCase.RelativePath,
                    testCase.InputFile,
                    testCase.CaseName,
                });

        [Theory]
        [MemberData(nameof(CsProjSnapshots))]
        public async Task Unified_cli_matches_every_legacy_csproj_snapshot(
            string relativePath,
            string inputFile,
            string caseName)
        {
            var actual = await CliSnapshotTestRunner.RunAndAssertAsync(
                GetCsProjFixtureRoot(),
                relativePath,
                inputFile,
                caseName,
                allowSkippedWhenUnchanged: true);
            GitSnapshot.Match(actual, typeof(LegacyCliSnapshotTests), caseName);
        }

        private static string GetCsProjFixtureRoot()
        {
            return Path.Combine(
                AppContext.BaseDirectory,
                "Formatter",
                "CsProj",
                "_files");
        }

    }
}
