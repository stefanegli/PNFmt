// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace PNFmt.Tests.Formatter.CsProj.TestFoundation
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    internal static class FormattingCaseSource
    {
        public static IEnumerable<(string RelativePath, string InputFile, string ExpectedFile, string CaseName)> Create(string outputRoot)
        {
            var inputRoot = Path.Combine(outputRoot, "input");
            var expectedRoot = Path.Combine(outputRoot, "expected");

            if (!Directory.Exists(inputRoot))
            {
                throw new InvalidOperationException($"Input folder not found: {inputRoot}");
            }

            foreach (var inputFile in Directory.GetFiles(inputRoot, "*.csproj", SearchOption.AllDirectories))
            {
                var relativePath = GetRelativePath(inputRoot, inputFile);
                var expectedFile = Path.Combine(expectedRoot, relativePath);

                if (!File.Exists(expectedFile))
                {
                    throw new InvalidOperationException($"Expected file not found: {expectedFile}");
                }

                var caseName = relativePath.Replace(Path.DirectorySeparatorChar, '/');
                yield return (relativePath, inputFile, expectedFile, caseName);
            }
        }

        private static string AppendDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private static string GetRelativePath(string basePath, string fullPath)
        {
            var baseUri = new Uri(AppendDirectorySeparator(basePath), UriKind.Absolute);
            var fullUri = new Uri(fullPath, UriKind.Absolute);
            var relativeUri = baseUri.MakeRelativeUri(fullUri);
            return Uri.UnescapeDataString(relativeUri.ToString())
                .Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
