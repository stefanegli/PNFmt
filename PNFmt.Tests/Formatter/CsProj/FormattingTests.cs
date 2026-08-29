// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace PNFmt.Tests.Formatter.CsProj
{
    using PNFmt;

    using PNFmt.Tests.Formatter.CsProj.TestFoundation;
    using PNFmt.Tests.Snapshots;

    using System;
    using System.Collections.Generic;
    using System.IO;

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
                caseName,
                allowSkippedWhenUnchanged: true);
            GitSnapshot.Match(actual, typeof(CsProjSnapshotTests), caseName);
        }

        internal class CsProjSnapshotData : TheoryDataBase<string, string, string>
        {
            public override IEnumerable<(string, string, string)> Create()
            {
                foreach (var testCase in FileSnapshotCaseSource.Create(GetFixtureRoot(), ".csproj"))
                {
                    yield return (testCase.InputFile, testCase.RelativePath, testCase.CaseName);
                }
            }
        }

        private static string GetFixtureRoot()
        {
            return Path.Combine(AppContext.BaseDirectory, "Formatter", "CsProj", "_files");
        }
    }
}
