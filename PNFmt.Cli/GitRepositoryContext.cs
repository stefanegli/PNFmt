// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;

namespace PNFmt.Cli
{
    internal sealed class GitRepositoryContext
    {
        private readonly HashSet<string> changedFiles;

        private GitRepositoryContext(string rootPath, IEnumerable<string> changedFiles)
        {
            this.RootPath = Path.GetFullPath(rootPath);
            this.changedFiles = new HashSet<string>(
                changedFiles.Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase);
        }

        public string RootPath { get; }

        public static GitRepositoryContext Discover(
            string startPath,
            bool includeChangedFiles = true)
        {
            try
            {
                var repositoryPath = Repository.Discover(startPath);
                if (repositoryPath is null)
                {
                    return null;
                }

                using (var repository = new Repository(repositoryPath))
                {
                    var rootPath = repository.Info.WorkingDirectory;
                    var files = includeChangedFiles
                        ? GetChangedFiles(repository, rootPath)
                        : Array.Empty<string>();
                    return new GitRepositoryContext(rootPath, files);
                }
            }
            catch (Exception ex) when (ex is LibGit2SharpException
                || ex is ArgumentException
                || ex is IOException
                || ex is UnauthorizedAccessException)
            {
                throw new GitRepositoryContextException(
                    $"Unable to read Git status: {ex.Message}",
                    ex);
            }
        }

        public bool Contains(string path)
        {
            var relativePath = Path.GetRelativePath(this.RootPath, Path.GetFullPath(path));
            return !Path.IsPathRooted(relativePath)
                && !string.Equals(relativePath, "..", StringComparison.Ordinal)
                && !relativePath.StartsWith(
                    ".." + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal);
        }

        public IReadOnlyCollection<string> GetChangedFiles(string directory, bool recursive)
        {
            var fullDirectory = Path.GetFullPath(directory);
            return this.changedFiles
                .Where(file => IsWithinDirectory(file, fullDirectory, recursive))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public bool IsChanged(string file)
        {
            return this.changedFiles.Contains(Path.GetFullPath(file));
        }

        private static bool IsWithinDirectory(string file, string directory, bool recursive)
        {
            var relativePath = Path.GetRelativePath(directory, file);
            if (Path.IsPathRooted(relativePath)
                || string.Equals(relativePath, "..", StringComparison.Ordinal)
                || relativePath.StartsWith(
                    ".." + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return recursive || string.IsNullOrEmpty(Path.GetDirectoryName(relativePath));
        }

        private static IReadOnlyCollection<string> GetChangedFiles(
            Repository repository,
            string rootPath)
        {
            var status = repository.RetrieveStatus(
                new StatusOptions
                {
                    IncludeUntracked = true,
                    RecurseUntrackedDirs = true,
                });
            return status
                .Where(entry => entry.State != FileStatus.Unaltered
                    && (entry.State & FileStatus.Ignored) == 0)
                .Select(entry => Path.Combine(rootPath, NormalizeGitPath(entry.FilePath)))
                .ToArray();
        }

        private static string NormalizeGitPath(string path)
        {
            return path.Replace('/', Path.DirectorySeparatorChar);
        }
    }

    internal sealed class GitRepositoryContextException : Exception
    {
        public GitRepositoryContextException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
