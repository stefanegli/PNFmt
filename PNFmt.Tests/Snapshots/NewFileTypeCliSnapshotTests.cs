// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PNFmt.Tests.Snapshots
{
    public sealed class NewFileTypeCliSnapshotTests
    {
        public static IEnumerable<object[]> IniSnapshots => CreateData("Ini");

        public static IEnumerable<object[]> SlnxSnapshots => CreateData("Slnx");

        [Theory]
        [MemberData(nameof(IniSnapshots))]
        public async Task Unified_cli_matches_ini_snapshot(
            string fixtureRoot,
            string relativePath,
            string inputFile,
            string expectedFile,
            string caseName)
        {
            await CliSnapshotTestRunner.AssertMatchesAsync(
                fixtureRoot,
                relativePath,
                inputFile,
                expectedFile,
                caseName);
        }

        [Theory]
        [MemberData(nameof(SlnxSnapshots))]
        public async Task Unified_cli_matches_slnx_snapshot(
            string fixtureRoot,
            string relativePath,
            string inputFile,
            string expectedFile,
            string caseName)
        {
            await CliSnapshotTestRunner.AssertMatchesAsync(
                fixtureRoot,
                relativePath,
                inputFile,
                expectedFile,
                caseName);
        }

        private static IEnumerable<object[]> CreateData(string formatterDirectory)
        {
            var fixtureRoot = Path.Combine(
                AppContext.BaseDirectory,
                "Formatter",
                formatterDirectory,
                "_files");
            return FileSnapshotCaseSource.Create(fixtureRoot)
                .Select(testCase => new object[]
                {
                    fixtureRoot,
                    testCase.RelativePath,
                    testCase.InputFile,
                    testCase.ExpectedFile,
                    testCase.CaseName,
                });
        }
    }
}
