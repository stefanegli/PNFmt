// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PNFmt;

namespace PNFmt.Cli
{
    internal sealed class TargetFileResolver
    {
        private static readonly HashSet<string> IgnoredRecursiveDirectories =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".git",
                ".vs",
                "artifacts",
                "bin",
                "node_modules",
                "obj",
            };

        private readonly FormatterRegistry activeFormatters;
        private readonly FormatterRegistry allFormatters;
        private readonly FilePatternMatcher filePatternMatcher;

        public TargetFileResolver(
            FormatterRegistry activeFormatters,
            FormatterRegistry allFormatters,
            FilePatternMatcher filePatternMatcher)
        {
            this.activeFormatters = activeFormatters
                ?? throw new ArgumentNullException(nameof(activeFormatters));
            this.allFormatters = allFormatters
                ?? throw new ArgumentNullException(nameof(allFormatters));
            this.filePatternMatcher = filePatternMatcher
                ?? throw new ArgumentNullException(nameof(filePatternMatcher));
        }

        public TargetFileResolution Resolve(
            IEnumerable<string> paths,
            bool recursive,
            GitRepositoryContext repository = null)
        {
            if (paths is null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var errors = new List<string>();
            var gitFiltered = false;
            foreach (var rawPath in paths)
            {
                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    continue;
                }

                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(rawPath);
                }
                catch (Exception ex) when (IsPathException(ex))
                {
                    errors.Add($"Unable to access path '{rawPath}': {ex.Message}");
                    continue;
                }

                if (File.Exists(fullPath))
                {
                    var isInRepository = repository is not null && repository.Contains(fullPath);
                    gitFiltered |= isInRepository;
                    if (!isInRepository || repository.IsChanged(fullPath))
                    {
                        this.AddFile(fullPath, Path.GetDirectoryName(fullPath), files, errors);
                    }

                    continue;
                }

                if (Directory.Exists(fullPath))
                {
                    if (repository is not null && repository.Contains(fullPath))
                    {
                        gitFiltered = true;
                        foreach (var file in repository.GetChangedFiles(fullPath, recursive))
                        {
                            if (File.Exists(file) && !IsInIgnoredDirectory(file, fullPath))
                            {
                                this.AddFile(
                                    file,
                                    fullPath,
                                    files,
                                    errors,
                                    reportUnsupported: false);
                            }
                        }
                    }
                    else
                    {
                        this.CollectDirectoryFiles(
                            fullPath,
                            fullPath,
                            recursive,
                            files,
                            errors);
                    }

                    continue;
                }

                errors.Add($"Path not found: {fullPath}");
            }

            return new TargetFileResolution(
                files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray(),
                errors,
                gitFiltered);
        }

        private static bool IsPathException(Exception exception)
        {
            return exception is ArgumentException
                || exception is IOException
                || exception is NotSupportedException
                || exception is UnauthorizedAccessException;
        }

        private static bool IsInIgnoredDirectory(string file, string rootDirectory)
        {
            var relativeDirectory = Path.GetDirectoryName(
                Path.GetRelativePath(rootDirectory, file));
            if (string.IsNullOrEmpty(relativeDirectory))
            {
                return false;
            }

            return relativeDirectory
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(IgnoredRecursiveDirectories.Contains);
        }

        private void AddFile(
            string file,
            string baseDirectory,
            HashSet<string> files,
            List<string> errors,
            bool reportUnsupported = true)
        {
            if (!this.filePatternMatcher.IsMatch(file, baseDirectory))
            {
                return;
            }

            if (this.activeFormatters.TryGetFormatter(file, out _))
            {
                files.Add(Path.GetFullPath(file));
            }
            else if (reportUnsupported && !this.allFormatters.TryGetFormatter(file, out _))
            {
                errors.Add($"Path is not a supported file type: {file}");
            }
        }

        private void CollectDirectoryFiles(
            string directoryPath,
            string rootDirectory,
            bool recursive,
            HashSet<string> files,
            List<string> errors)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(
                    directoryPath,
                    "*",
                    SearchOption.TopDirectoryOnly))
                {
                    this.AddFile(file, rootDirectory, files, errors, reportUnsupported: false);
                }

                if (!recursive)
                {
                    return;
                }

                foreach (var childDirectory in Directory.EnumerateDirectories(
                    directoryPath,
                    "*",
                    SearchOption.TopDirectoryOnly))
                {
                    if (IgnoredRecursiveDirectories.Contains(Path.GetFileName(childDirectory)))
                    {
                        continue;
                    }

                    if ((File.GetAttributes(childDirectory) & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    this.CollectDirectoryFiles(
                        childDirectory,
                        rootDirectory,
                        true,
                        files,
                        errors);
                }
            }
            catch (Exception ex) when (IsPathException(ex))
            {
                errors.Add($"Unable to access path '{directoryPath}': {ex.Message}");
            }
        }
    }

    internal sealed class TargetFileResolution
    {
        public TargetFileResolution(
            IReadOnlyList<string> files,
            IReadOnlyList<string> errors,
            bool gitFiltered)
        {
            this.Files = files ?? throw new ArgumentNullException(nameof(files));
            this.Errors = errors ?? throw new ArgumentNullException(nameof(errors));
            this.GitFiltered = gitFiltered;
        }

        public IReadOnlyList<string> Files { get; }

        public bool GitFiltered { get; }

        public IReadOnlyList<string> Errors { get; }
    }
}
