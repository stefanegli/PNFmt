// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using PNFmt.Tests.Snapshots;

using Xunit;

namespace PNFmt.Tests.Formatter.Slnx
{
    public sealed class SlnxSnapshotTests
    {
        public static IEnumerable<object[]> RejectedSnapshots =>
            GetCases(rejected: true);

        public static IEnumerable<object[]> Snapshots =>
            GetCases(rejected: false);

        [Theory]
        [MemberData(nameof(Snapshots))]
        public void Formatter_matches_snapshot(
            string relativePath,
            string inputFile,
            string caseName)
        {
            var actual = FormatterSnapshotTestRunner.FormatAndAssertIdempotent(
                new SlnxFormatter(),
                GetFixtureRoot(),
                relativePath,
                inputFile,
                caseName);
            GitSnapshot.Match(actual, typeof(SlnxSnapshotTests), caseName);
        }

        [Theory]
        [MemberData(nameof(RejectedSnapshots))]
        public void Rejected_files_remain_unchanged(string relativePath, string inputFile, string caseName)
        {
            using (var staged = TestDirectory.CopyFrom(Path.Combine(GetFixtureRoot(), "input")))
            {
                var path = staged.GetPath(relativePath);
                var original = File.ReadAllBytes(inputFile);
                foreach (var write in new[] { false, true })
                {
                    Assert.Throws<XmlException>(() => new SlnxFormatter().Format(
                        new FileFormatRequest(path, write, false, NullFormatterLog.Instance)));
                    Assert.Equal(original, File.ReadAllBytes(path));
                }

                GitSnapshot.Match(File.ReadAllText(path), typeof(SlnxSnapshotTests), caseName);
            }
        }

        private static IEnumerable<object[]> GetCases(bool rejected) =>
            FileSnapshotCaseSource.Create(GetFixtureRoot(), ".slnx")
                .Where(testCase => testCase.RelativePath.StartsWith("Rejected" + Path.DirectorySeparatorChar, StringComparison.Ordinal) == rejected)
                .Select(testCase => new object[]
                {
                    testCase.RelativePath,
                    testCase.InputFile,
                    testCase.CaseName,
                });

        private static string GetFixtureRoot()
        {
            return Path.Combine(AppContext.BaseDirectory, "Formatter", "Slnx", "_files");
        }
    }
}
