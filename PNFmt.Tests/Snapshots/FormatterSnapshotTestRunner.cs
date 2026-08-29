// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using Xunit;

namespace PNFmt.Tests.Snapshots
{
    internal static class FormatterSnapshotTestRunner
    {
        public static void AssertMatches(
            IFileFormatter formatter,
            string fixtureRoot,
            string relativePath,
            string inputFile,
            string expectedFile,
            string caseName)
        {
            var inputRoot = Path.Combine(fixtureRoot, "input");
            using (var stagedInput = TemporarySnapshotDirectory.CopyFrom(inputRoot))
            {
                var stagedFile = stagedInput.GetPath(relativePath);
                var expectedText = File.ReadAllText(expectedFile);
                var changed = !string.Equals(
                    File.ReadAllText(inputFile),
                    expectedText,
                    StringComparison.Ordinal);

                var firstRun = formatter.Format(
                    new FileFormatRequest(stagedFile, true, false, NullFormatterLog.Instance));
                var secondRun = formatter.Format(
                    new FileFormatRequest(stagedFile, true, false, NullFormatterLog.Instance));

                Assert.Equal(changed ? FileFormatStatus.Updated : FileFormatStatus.Unchanged, firstRun.Status);
                Assert.Equal(FileFormatStatus.Unchanged, secondRun.Status);
                Assert.True(
                    string.Equals(expectedText, File.ReadAllText(stagedFile), StringComparison.Ordinal),
                    $"Snapshot mismatch for {caseName}.");
            }
        }

        private sealed class NullFormatterLog : IFormatterLog
        {
            public static NullFormatterLog Instance { get; } = new NullFormatterLog();

            public void Write(Exception exception)
            {
            }

            public void WriteLine(string message)
            {
            }
        }
    }
}
