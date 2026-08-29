// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using Xunit;

namespace PNFmt.Tests.Snapshots
{
    internal static class FormatterSnapshotTestRunner
    {
        public static string FormatAndAssertIdempotent(
            IFileFormatter formatter,
            string fixtureRoot,
            string relativePath,
            string inputFile,
            string caseName,
            bool allowSkippedWhenUnchanged = false)
        {
            var inputRoot = Path.Combine(fixtureRoot, "input");
            using (var stagedInput = TemporarySnapshotDirectory.CopyFrom(inputRoot))
            {
                var stagedFile = stagedInput.GetPath(relativePath);
                var inputText = File.ReadAllText(inputFile);

                var firstRun = formatter.Format(
                    new FileFormatRequest(stagedFile, true, false, NullFormatterLog.Instance));
                var actual = File.ReadAllText(stagedFile);
                var secondRun = formatter.Format(
                    new FileFormatRequest(stagedFile, true, false, NullFormatterLog.Instance));

                var changed = !string.Equals(inputText, actual, StringComparison.Ordinal);
                if (changed)
                {
                    Assert.Equal(FileFormatStatus.Updated, firstRun.Status);
                }
                else
                {
                    AssertUnchangedOrSkipped(firstRun.Status, allowSkippedWhenUnchanged, caseName);
                }

                AssertUnchangedOrSkipped(secondRun.Status, allowSkippedWhenUnchanged, caseName);
                Assert.Equal(actual, File.ReadAllText(stagedFile));
                return actual;
            }
        }

        private static void AssertUnchangedOrSkipped(
            FileFormatStatus status,
            bool allowSkipped,
            string caseName)
        {
            Assert.True(
                status == FileFormatStatus.Unchanged
                || (allowSkipped && status == FileFormatStatus.Skipped),
                $"Expected unchanged status for {caseName}, but received {status}.");
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
