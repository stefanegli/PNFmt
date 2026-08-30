// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PNFmt.Cli
{
    internal sealed class FilePatternMatcher
    {
        private readonly IReadOnlyList<Regex> patterns;

        public FilePatternMatcher(IEnumerable<string> patterns)
        {
            if (patterns is null)
            {
                throw new ArgumentNullException(nameof(patterns));
            }

            var compiledPatterns = new List<Regex>();
            foreach (var pattern in patterns)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    throw new ArgumentException(
                        "File patterns cannot be empty.",
                        nameof(patterns));
                }

                compiledPatterns.Add(
                    new Regex(
                        CreateRegexPattern(pattern),
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
            }

            this.patterns = compiledPatterns.AsReadOnly();
        }

        public bool IsMatch(string filePath, string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A file path is required.", nameof(filePath));
            }

            if (this.patterns.Count == 0)
            {
                return true;
            }

            var fullPath = Path.GetFullPath(filePath);
            var resolvedBaseDirectory = baseDirectory ?? Environment.CurrentDirectory;
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NormalizePath(fullPath),
                NormalizePath(Path.GetFileName(fullPath)),
                NormalizePath(Path.GetRelativePath(resolvedBaseDirectory, fullPath)),
                NormalizePath(Path.GetRelativePath(Environment.CurrentDirectory, fullPath)),
            };

            foreach (var candidate in candidates)
            {
                foreach (var pattern in this.patterns)
                {
                    if (pattern.IsMatch(candidate))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string CreateRegexPattern(string pattern)
        {
            var normalized = NormalizePath(pattern.Trim());
            while (normalized.StartsWith("./", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(2);
            }

            var builder = new StringBuilder("^");
            for (var index = 0; index < normalized.Length; index++)
            {
                var character = normalized[index];
                if (character == '*')
                {
                    var isDoubleStar = index + 1 < normalized.Length
                        && normalized[index + 1] == '*';
                    if (isDoubleStar)
                    {
                        index++;
                        if (index + 1 < normalized.Length && normalized[index + 1] == '/')
                        {
                            index++;
                            builder.Append("(?:.*/)?");
                        }
                        else
                        {
                            builder.Append(".*");
                        }
                    }
                    else
                    {
                        builder.Append("[^/]*");
                    }

                    continue;
                }

                if (character == '?')
                {
                    builder.Append("[^/]");
                    continue;
                }

                builder.Append(Regex.Escape(character.ToString()));
            }

            builder.Append("$");
            return builder.ToString();
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
