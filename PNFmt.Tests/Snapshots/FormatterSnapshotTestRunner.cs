// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System.IO;
using System.Linq;

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
            using (var stagedInput = TestDirectory.CopyFrom(inputRoot))
            {
                var stagedFile = stagedInput.GetPath(relativePath);
                var inputBytes = File.ReadAllBytes(inputFile);

                var preview = formatter.Format(
                    new FileFormatRequest(stagedFile, false, false, NullFormatterLog.Instance));
                Assert.Equal(inputBytes, File.ReadAllBytes(stagedFile));
                var firstRun = formatter.Format(
                    new FileFormatRequest(stagedFile, true, false, NullFormatterLog.Instance));
                Assert.Equal(preview.Status, firstRun.Status);
                var actual = File.ReadAllText(stagedFile);
                var formattedBytes = File.ReadAllBytes(stagedFile);
                var secondRun = formatter.Format(
                    new FileFormatRequest(stagedFile, true, false, NullFormatterLog.Instance));

                var changed = !inputBytes.SequenceEqual(formattedBytes);
                if (changed)
                {
                    Assert.Equal(FileFormatStatus.Updated, firstRun.Status);
                }
                else
                {
                    AssertUnchangedOrSkipped(firstRun.Status, allowSkippedWhenUnchanged, caseName);
                }

                AssertUnchangedOrSkipped(secondRun.Status, allowSkippedWhenUnchanged, caseName);
                Assert.Equal(formattedBytes, File.ReadAllBytes(stagedFile));
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

    }
}
