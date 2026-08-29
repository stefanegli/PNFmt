// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PNFmt.Tests.Snapshots
{
    internal static class FileSnapshotCaseSource
    {
        public static IEnumerable<(string RelativePath, string InputFile, string ExpectedFile, string CaseName)> Create(
            string fixtureRoot)
        {
            var inputRoot = Path.Combine(fixtureRoot, "input");
            var expectedRoot = Path.Combine(fixtureRoot, "expected");
            if (!Directory.Exists(inputRoot))
            {
                throw new InvalidOperationException($"Input folder not found: {inputRoot}");
            }

            if (!Directory.Exists(expectedRoot))
            {
                throw new InvalidOperationException($"Expected folder not found: {expectedRoot}");
            }

            var inputs = GetRelativeFiles(inputRoot);
            var expected = GetRelativeFiles(expectedRoot);
            var missingExpected = inputs.Except(expected, StringComparer.OrdinalIgnoreCase).ToArray();
            var missingInputs = expected.Except(inputs, StringComparer.OrdinalIgnoreCase).ToArray();
            if (missingExpected.Length > 0 || missingInputs.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Snapshot fixture mismatch. Missing expected: {string.Join(", ", missingExpected)}. "
                    + $"Missing input: {string.Join(", ", missingInputs)}.");
            }

            foreach (var relativePath in inputs)
            {
                yield return (
                    relativePath,
                    Path.Combine(inputRoot, relativePath),
                    Path.Combine(expectedRoot, relativePath),
                    relativePath.Replace(Path.DirectorySeparatorChar, '/'));
            }
        }

        private static string[] GetRelativeFiles(string root)
        {
            return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(root, file))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
