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
            new HashSet<string>(PathComparison.Comparer)
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
            bool allFiles = false)
        {
            if (paths is null)
            {
                throw new ArgumentNullException(nameof(paths));
            }

            var files = new HashSet<string>(PathComparison.Comparer);
            var errors = new List<string>();
            var settings = new TargetRepositorySettings(allFiles);
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
                    var repository = settings.ForDirectory(Path.GetDirectoryName(fullPath));
                    if (repository is null || repository.IsChanged(fullPath))
                    {
                        this.AddFile(fullPath, Path.GetDirectoryName(fullPath), files, errors);
                    }

                    continue;
                }

                if (Directory.Exists(fullPath))
                {
                    var repository = settings.ForDirectory(fullPath);
                    if (repository is not null)
                    {
                        this.CollectChangedFiles(repository, fullPath, fullPath, recursive, files, errors);
                    }
                    else
                    {
                        this.CollectDirectoryFiles(
                            fullPath,
                            fullPath,
                            recursive,
                            files,
                            errors,
                            settings);
                    }

                    continue;
                }

                errors.Add($"Path not found: {fullPath}");
            }

            return new TargetFileResolution(
                files.OrderBy(path => path, PathComparison.Comparer).ToArray(),
                errors,
                settings.GitFiltered,
                settings.MaxCpuCount);
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

            try
            {
                if (this.allFormatters.TryGetConfiguredFormatter(file, out var formatter))
                {
                    if (formatter is not null
                        ? this.activeFormatters.Formatters.Contains(formatter)
                        : this.activeFormatters.TryGetFormatter(file, out _))
                    {
                        files.Add(Path.GetFullPath(file));
                    }
                }
                else if (reportUnsupported)
                {
                    errors.Add($"Path is not a supported file type: {file}");
                }
            }
            catch (InvalidDataException)
            {
                // Let the runner report configuration errors as per-file failures.
                files.Add(Path.GetFullPath(file));
            }
        }

        private void CollectChangedFiles(
            GitRepositoryContext repository,
            string directory,
            string rootDirectory,
            bool recursive,
            HashSet<string> files,
            List<string> errors)
        {
            foreach (var file in repository.GetChangedFiles(directory, recursive))
            {
                if (File.Exists(file) && !IsInIgnoredDirectory(file, directory))
                {
                    this.AddFile(file, rootDirectory, files, errors, reportUnsupported: false);
                }
            }
        }

        private void CollectDirectoryFiles(
            string directoryPath,
            string rootDirectory,
            bool recursive,
            HashSet<string> files,
            List<string> errors,
            TargetRepositorySettings settings)
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

                    var gitPath = Path.Combine(childDirectory, ".git");
                    var repository = Directory.Exists(gitPath) || File.Exists(gitPath)
                        ? settings.ForDirectory(childDirectory)
                        : null;
                    if (repository is not null)
                    {
                        this.CollectChangedFiles(repository, childDirectory, rootDirectory, true, files, errors);
                    }
                    else
                    {
                        this.CollectDirectoryFiles(childDirectory, rootDirectory, true, files, errors, settings);
                    }
                }
            }
            catch (Exception ex) when (IsPathException(ex))
            {
                errors.Add($"Unable to access path '{directoryPath}': {ex.Message}");
            }
        }
    }

    internal sealed class TargetRepositorySettings
    {
        private readonly bool allFiles;
        private readonly Dictionary<string, GitRepositoryContext> repositories = new Dictionary<string, GitRepositoryContext>(PathComparison.Comparer);
        private readonly Dictionary<string, PNFmtConfiguration> configurations = new Dictionary<string, PNFmtConfiguration>(PathComparison.Comparer);

        public TargetRepositorySettings(bool allFiles)
        {
            this.allFiles = allFiles;
        }

        public bool GitFiltered { get; private set; }

        public int MaxCpuCount => this.configurations.Values.Select(configuration => configuration.MaxCpuCount).DefaultIfEmpty(1).Min();

        public GitRepositoryContext ForDirectory(string directory)
        {
            var repository = GitRepositoryContext.Discover(directory, !this.allFiles, this.repositories);
            var configurationRoot = repository?.RootPath ?? directory;
            if (!this.configurations.ContainsKey(configurationRoot))
            {
                this.configurations.Add(configurationRoot, PNFmtConfiguration.Load(configurationRoot));
            }

            if (this.allFiles)
            {
                return null;
            }

            this.GitFiltered |= repository is not null;
            return repository;
        }
    }

    internal sealed class TargetFileResolution
    {
        public TargetFileResolution(
            IReadOnlyList<string> files,
            IReadOnlyList<string> errors,
            bool gitFiltered,
            int maxCpuCount)
        {
            this.Files = files ?? throw new ArgumentNullException(nameof(files));
            this.Errors = errors ?? throw new ArgumentNullException(nameof(errors));
            this.GitFiltered = gitFiltered;
            this.MaxCpuCount = maxCpuCount;
        }

        public IReadOnlyList<string> Files { get; }

        public bool GitFiltered { get; }

        public int MaxCpuCount { get; }

        public IReadOnlyList<string> Errors { get; }
    }
}
