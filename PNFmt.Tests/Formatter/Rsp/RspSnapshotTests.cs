// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PNFmt.Tests.Snapshots;
using Xunit;

namespace PNFmt.Tests.Formatter.Rsp
{
    public sealed class RspSnapshotTests
    {
        public static IEnumerable<object[]> Snapshots =>
            FileSnapshotCaseSource.Create(GetFixtureRoot(), ".rsp")
                .Select(testCase => new object[]
                {
                    testCase.RelativePath,
                    testCase.InputFile,
                    testCase.CaseName,
                });

        [Theory]
        [MemberData(nameof(Snapshots))]
        public void Formatter_matches_snapshot(
            string relativePath,
            string inputFile,
            string caseName)
        {
            var actual = FormatterSnapshotTestRunner.FormatAndAssertIdempotent(
                new RspFormatter(),
                GetFixtureRoot(),
                relativePath,
                inputFile,
                caseName);
            GitSnapshot.Match(actual, typeof(RspSnapshotTests), caseName);
        }

        private static string GetFixtureRoot()
        {
            return Path.Combine(AppContext.BaseDirectory, "Formatter", "Rsp", "_files");
        }
    }
}
