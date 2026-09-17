// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using PNFmt.Tests.Snapshots;

using Xunit;

namespace PNFmt.Tests.Formatter.CSharp
{
    public sealed class CSharpSnapshotTests
    {
        public static IEnumerable<object[]> Snapshots =>
            FileSnapshotCaseSource.Create(GetFixtureRoot(), ".cs")
                .Select(testCase => new object[] { testCase.RelativePath, testCase.InputFile, testCase.CaseName });

        [Theory]
        [MemberData(nameof(Snapshots))]
        public void Formatter_matches_snapshot(string relativePath, string inputFile, string caseName)
        {
            var actual = FormatterSnapshotTestRunner.FormatAndAssertIdempotent(
                new CSharpFormatter(), GetFixtureRoot(), relativePath, inputFile, caseName);
            GitSnapshot.Match(actual, typeof(CSharpSnapshotTests), caseName);
        }

        private static string GetFixtureRoot()
        {
            return Path.Combine(AppContext.BaseDirectory, "Formatter", "CSharp", "_files");
        }
    }
}
