// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using PNFmt.Tests.Snapshots;

using Xunit;

namespace PNFmt.Tests.Formatter.Xml
{
    public sealed class XmlSnapshotTests
    {
        public static IEnumerable<object[]> RejectedSnapshots => GetCases(rejected: true);

        public static IEnumerable<object[]> Snapshots => GetCases(rejected: false);

        [Theory]
        [MemberData(nameof(Snapshots))]
        public void Formatter_matches_snapshot(string relativePath, string inputFile, string caseName)
        {
            IFileFormatter formatter = Path.GetExtension(inputFile) == ".xaml" ? new XamlFormatter() : new XmlFormatter();
            var actual = FormatterSnapshotTestRunner.FormatAndAssertIdempotent(
                formatter, GetFixtureRoot(), relativePath, inputFile, caseName);
            GitSnapshot.Match(actual, typeof(XmlSnapshotTests), caseName);
        }

        [Theory]
        [MemberData(nameof(RejectedSnapshots))]
        public void Rejected_files_remain_unchanged(string relativePath, string inputFile, string caseName)
        {
            using (var staged = TestDirectory.CopyFrom(Path.Combine(GetFixtureRoot(), "input")))
            {
                var path = staged.GetPath(relativePath);
                var original = File.ReadAllBytes(inputFile);
                var xaml = Path.GetExtension(inputFile) == ".xaml";
                IFileFormatter formatter = xaml ? new XamlFormatter() : new XmlFormatter();
                foreach (var write in new[] { false, true })
                {
                    var result = formatter.Format(new FileFormatRequest(path, write, false, NullFormatterLog.Instance));
                    Assert.Equal(FileFormatStatus.Skipped, result.Status);
                    Assert.Equal(xaml ? "XAML001" : "XML001", Assert.Single(result.Diagnostics).Code);
                    Assert.Equal(original, File.ReadAllBytes(path));
                }

                GitSnapshot.Match(File.ReadAllText(path), typeof(XmlSnapshotTests), caseName);
            }
        }

        private static IEnumerable<object[]> GetCases(bool rejected) => new[] { ".xml", ".xaml" }
            .SelectMany(extension => FileSnapshotCaseSource.Create(GetFixtureRoot(), extension))
            .Where(testCase => testCase.RelativePath.StartsWith("Rejected" + Path.DirectorySeparatorChar, StringComparison.Ordinal) == rejected)
            .Select(testCase => new object[] { testCase.RelativePath, testCase.InputFile, testCase.CaseName });

        private static string GetFixtureRoot()
        {
            return Path.Combine(AppContext.BaseDirectory, "Formatter", "Xml", "_files");
        }
    }
}
