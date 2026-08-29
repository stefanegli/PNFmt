// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
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
            await CliSnapshotTestRunner.AssertMatchesAsync(
                GetCsProjFixtureRoot(),
                relativePath,
                inputFile,
                expectedFile,
                caseName,
                allowSkippedWhenUnchanged: true);
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
