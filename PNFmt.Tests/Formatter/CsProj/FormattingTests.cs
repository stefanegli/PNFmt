// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace PNFmt.Tests.Formatter.CsProj
{
    using System;
    using System.IO;

    using PNFmt;
    using PNFmt.Tests.Snapshots;

    using Xunit;

    public class CsProjSnapshotTests
    {
        [Theory]
        [ClassData(typeof(CsProjSnapshotData))]
        public void Files_are_processed_correctly(string inputFile, string relativePath, string caseName)
        {
            var fixtureRoot = GetFixtureRoot();
            var actual = FormatterSnapshotTestRunner.FormatAndAssertIdempotent(
                new CsProjFormatter(),
                fixtureRoot,
                relativePath,
                inputFile,
                caseName);
            GitSnapshot.Match(actual, typeof(CsProjSnapshotTests), caseName);
        }

        private static string GetFixtureRoot()
        {
            return Path.Combine(AppContext.BaseDirectory, "Formatter", "CsProj", "_files");
        }

        internal class CsProjSnapshotData : TheoryData<string, string, string>
        {
            public CsProjSnapshotData()
            {
                foreach (var testCase in FileSnapshotCaseSource.Create(GetFixtureRoot(), ".csproj", ".props", ".targets", ".proj"))
                {
                    this.Add(testCase.InputFile, testCase.RelativePath, testCase.CaseName);
                }
            }
        }
    }
}
