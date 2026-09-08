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
        public static IEnumerable<object[]> Snapshots => new[] { ".xml", ".xaml" }
            .SelectMany(extension => FileSnapshotCaseSource.Create(GetFixtureRoot(), extension))
            .Select(testCase => new object[] { testCase.RelativePath, testCase.InputFile, testCase.CaseName });

        [Theory]
        [MemberData(nameof(Snapshots))]
        public void Formatter_matches_snapshot(string relativePath, string inputFile, string caseName)
        {
            IFileFormatter formatter = Path.GetExtension(inputFile) == ".xaml" ? new XamlFormatter() : new XmlFormatter();
            var actual = FormatterSnapshotTestRunner.FormatAndAssertIdempotent(
                formatter, GetFixtureRoot(), relativePath, inputFile, caseName);
            GitSnapshot.Match(actual, typeof(XmlSnapshotTests), caseName);
        }

        private static string GetFixtureRoot()
        {
            return Path.Combine(AppContext.BaseDirectory, "Formatter", "Xml", "_files");
        }
    }
}
