// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using LibGit2Sharp;
using Xunit.Sdk;

namespace PNFmt.Tests.Snapshots
{
    internal static class GitSnapshot
    {
        private const FileStatus IndexChanges =
            FileStatus.NewInIndex
            | FileStatus.ModifiedInIndex
            | FileStatus.DeletedFromIndex
            | FileStatus.RenamedInIndex
            | FileStatus.TypeChangeInIndex;

        public static void Match(
            string actual,
            Type testClass,
            string snapshotName,
            [CallerMemberName] string testName = null)
        {
            if (testClass is null)
            {
                throw new ArgumentNullException(nameof(testClass));
            }

            var repositoryPath = Repository.Discover(AppContext.BaseDirectory);
            if (repositoryPath is null)
            {
                throw new InvalidOperationException(
                    $"No Git repository contains the test output directory '{AppContext.BaseDirectory}'.");
            }

            using (var repository = new Repository(repositoryPath))
            {
                var snapshotPath = BuildSnapshotPath(
                    repository.Info.WorkingDirectory,
                    testClass,
                    testName,
                    snapshotName);
                Verify(snapshotPath, actual);
            }
        }

        internal static void Verify(string snapshotPath, string actual)
        {
            if (snapshotPath is null)
            {
                throw new ArgumentNullException(nameof(snapshotPath));
            }

            if (actual is null)
            {
                throw new ArgumentNullException(nameof(actual));
            }

            var repositoryPath = Repository.Discover(GetExistingParent(snapshotPath));
            if (repositoryPath is null)
            {
                throw new InvalidOperationException(
                    $"Snapshot path is not inside a Git repository: {snapshotPath}");
            }

            using (var repository = new Repository(repositoryPath))
            {
                var fullSnapshotPath = Path.GetFullPath(snapshotPath);
                var workingDirectory = Path.GetFullPath(repository.Info.WorkingDirectory);
                EnsureInsideRepository(workingDirectory, fullSnapshotPath);

                var relativePath = Path.GetRelativePath(workingDirectory, fullSnapshotPath)
                    .Replace(Path.DirectorySeparatorChar, '/');
                var statusBeforeRun = repository.RetrieveStatus(relativePath);
                var useIndex = (statusBeforeRun & IndexChanges) != 0;

                Directory.CreateDirectory(Path.GetDirectoryName(fullSnapshotPath));
                File.WriteAllText(fullSnapshotPath, actual, new UTF8Encoding(false));

                using (var patch = CreatePatch(repository, relativePath, useIndex))
                {
                    if (string.IsNullOrEmpty(patch.Content))
                    {
                        return;
                    }

                    var baseline = useIndex ? "staged snapshot" : "current branch";
                    throw new XunitException(
                        $"Snapshot '{relativePath}' differs from the {baseline}.{Environment.NewLine}"
                        + $"Review the file and stage it with: git add -- \"{relativePath}\"{Environment.NewLine}"
                        + patch.Content);
                }
            }
        }

        internal static string BuildSnapshotPath(
            string workingDirectory,
            Type testClass,
            string testName,
            string snapshotName)
        {
            if (string.IsNullOrWhiteSpace(testName))
            {
                throw new ArgumentException("A test name is required.", nameof(testName));
            }

            if (string.IsNullOrWhiteSpace(snapshotName))
            {
                throw new ArgumentException("A snapshot name is required.", nameof(snapshotName));
            }

            const string TestNamespace = "PNFmt.Tests";
            var classNamespace = testClass.Namespace ?? string.Empty;
            if (!string.Equals(classNamespace, TestNamespace, StringComparison.Ordinal)
                && !classNamespace.StartsWith(TestNamespace + ".", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Test class namespace must start with '{TestNamespace}'.",
                    nameof(testClass));
            }

            var snapshotParts = snapshotName.Split(
                new[] { '/', '\\' },
                StringSplitOptions.RemoveEmptyEntries);
            if (snapshotParts.Any(part => part == "." || part == ".."))
            {
                throw new ArgumentException(
                    "Snapshot names cannot contain relative directory segments.",
                    nameof(snapshotName));
            }

            var namespaceSuffix = classNamespace.Length == TestNamespace.Length
                ? Array.Empty<string>()
                : classNamespace.Substring(TestNamespace.Length + 1).Split('.');
            var pathParts = new[] { workingDirectory, "Snapshots", "PNFmt.Tests" }
                .Concat(namespaceSuffix)
                .Concat(new[] { testClass.Name, testName })
                .Concat(snapshotParts)
                .ToArray();
            var snapshotPath = Path.GetFullPath(Path.Combine(pathParts));
            EnsureInsideRepository(workingDirectory, snapshotPath);
            return snapshotPath;
        }

        private static Patch CreatePatch(Repository repository, string relativePath, bool useIndex)
        {
            var paths = new[] { relativePath };
            if (useIndex)
            {
                return repository.Diff.Compare<Patch>(paths, includeUntracked: true);
            }

            var headTree = repository.Head.Tip?.Tree;
            if (headTree?[relativePath]?.TargetType == TreeEntryTargetType.Blob)
            {
                return repository.Diff.Compare<Patch>(
                    headTree,
                    DiffTargets.WorkingDirectory,
                    paths);
            }

            return repository.Diff.Compare<Patch>(paths, includeUntracked: true);
        }

        private static void EnsureInsideRepository(string workingDirectory, string snapshotPath)
        {
            var root = workingDirectory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? workingDirectory
                : workingDirectory + Path.DirectorySeparatorChar;
            if (!snapshotPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Snapshot path must be inside the repository: {snapshotPath}");
            }
        }

        private static string GetExistingParent(string path)
        {
            var candidate = File.Exists(path) || Directory.Exists(path)
                ? path
                : Path.GetDirectoryName(Path.GetFullPath(path));
            while (candidate is not null && !Directory.Exists(candidate))
            {
                candidate = Path.GetDirectoryName(candidate);
            }

            return candidate ?? Path.GetPathRoot(Path.GetFullPath(path));
        }
    }
}
