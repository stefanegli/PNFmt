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
            string caseName)
        {
            var actual = await CliSnapshotTestRunner.RunAndAssertAsync(
                fixtureRoot,
                relativePath,
                inputFile,
                caseName);
            GitSnapshot.Match(actual, typeof(NewFileTypeCliSnapshotTests), caseName);
        }

        [Theory]
        [MemberData(nameof(SlnxSnapshots))]
        public async Task Unified_cli_matches_slnx_snapshot(
            string fixtureRoot,
            string relativePath,
            string inputFile,
            string caseName)
        {
            var actual = await CliSnapshotTestRunner.RunAndAssertAsync(
                fixtureRoot,
                relativePath,
                inputFile,
                caseName);
            GitSnapshot.Match(actual, typeof(NewFileTypeCliSnapshotTests), caseName);
        }

        private static IEnumerable<object[]> CreateData(string formatterDirectory)
        {
            var fixtureRoot = Path.Combine(
                AppContext.BaseDirectory,
                "Formatter",
                formatterDirectory,
                "_files");
            var extensions = string.Equals(formatterDirectory, "Ini", StringComparison.Ordinal)
                ? new[] { ".editorconfig", ".ini" }
                : new[] { ".slnx" };
            return FileSnapshotCaseSource.Create(fixtureRoot, extensions)
                .Select(testCase => new object[]
                {
                    fixtureRoot,
                    testCase.RelativePath,
                    testCase.InputFile,
                    testCase.CaseName,
                });
        }
    }
}
