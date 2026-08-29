// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PNFmt.Tests.Snapshots
{
    internal static class FileSnapshotCaseSource
    {
        public static IEnumerable<(string RelativePath, string InputFile, string CaseName)> Create(
            string fixtureRoot,
            params string[] extensions)
        {
            var inputRoot = Path.Combine(fixtureRoot, "input");
            if (!Directory.Exists(inputRoot))
            {
                throw new InvalidOperationException($"Input folder not found: {inputRoot}");
            }

            if (extensions is null || extensions.Length == 0)
            {
                throw new ArgumentException("At least one file extension is required.", nameof(extensions));
            }

            var inputs = GetRelativeFiles(inputRoot)
                .Where(path => extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase));
            foreach (var relativePath in inputs)
            {
                yield return (
                    relativePath,
                    Path.Combine(inputRoot, relativePath),
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
