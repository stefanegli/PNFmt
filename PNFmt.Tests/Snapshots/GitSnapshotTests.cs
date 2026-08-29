// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using LibGit2Sharp;
using Xunit;
using Xunit.Sdk;

namespace PNFmt.Tests.Snapshots
{
    public sealed class GitSnapshotTests
    {
        [Fact]
        public void Snapshot_path_mirrors_namespace_class_method_and_case()
        {
            var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repository"));

            var path = GitSnapshot.BuildSnapshotPath(
                root,
                typeof(GitSnapshotTests),
                "Example_test",
                "folder/example.txt");

            Assert.Equal(
                Path.Combine(
                    root,
                    "Snapshots",
                    "PNFmt.Tests",
                    "Snapshots",
                    nameof(GitSnapshotTests),
                    "Example_test",
                    "folder",
                    "example.txt"),
                path);
        }

        [Fact]
        public void Committed_snapshot_passes_when_output_is_unchanged()
        {
            using (var repository = TemporaryGitRepository.Create("approved\n"))
            {
                GitSnapshot.Verify(repository.SnapshotPath, "approved\n");

                Assert.Equal(FileStatus.Unaltered, repository.Status);
            }
        }

        [Fact]
        public void Changed_snapshot_is_overwritten_and_reports_branch_diff()
        {
            using (var repository = TemporaryGitRepository.Create("approved\n"))
            {
                var exception = Assert.Throws<XunitException>(
                    () => GitSnapshot.Verify(repository.SnapshotPath, "current\n"));

                Assert.Equal("current\n", File.ReadAllText(repository.SnapshotPath));
                Assert.Contains("differs from the current branch", exception.Message);
                Assert.Contains("-approved", exception.Message);
                Assert.Contains("+current", exception.Message);
            }
        }

        [Fact]
        public void Unstaged_edits_are_not_used_as_the_approval_baseline()
        {
            using (var repository = TemporaryGitRepository.Create("committed\n"))
            {
                File.WriteAllText(repository.SnapshotPath, "unstaged\n");

                var exception = Assert.Throws<XunitException>(
                    () => GitSnapshot.Verify(repository.SnapshotPath, "current\n"));

                Assert.Contains("-committed", exception.Message);
                Assert.Contains("+current", exception.Message);
                Assert.DoesNotContain("unstaged", exception.Message);
            }
        }

        [Fact]
        public void Staged_snapshot_becomes_the_approval_baseline()
        {
            using (var repository = TemporaryGitRepository.Create("committed\n"))
            {
                File.WriteAllText(repository.SnapshotPath, "approved\n");
                repository.StageSnapshot();

                GitSnapshot.Verify(repository.SnapshotPath, "approved\n");

                Assert.True((repository.Status & FileStatus.ModifiedInIndex) != 0);
                Assert.True((repository.Status & FileStatus.ModifiedInWorkdir) == 0);
            }
        }

        [Fact]
        public void Changes_after_staging_are_compared_with_the_index()
        {
            using (var repository = TemporaryGitRepository.Create("committed\n"))
            {
                File.WriteAllText(repository.SnapshotPath, "staged\n");
                repository.StageSnapshot();

                var exception = Assert.Throws<XunitException>(
                    () => GitSnapshot.Verify(repository.SnapshotPath, "current\n"));

                Assert.Contains("differs from the staged snapshot", exception.Message);
                Assert.Contains("-staged", exception.Message);
                Assert.Contains("+current", exception.Message);
                Assert.DoesNotContain("committed", exception.Message);
            }
        }

        [Fact]
        public void New_snapshot_fails_until_it_is_staged()
        {
            using (var repository = TemporaryGitRepository.CreateWithoutSnapshot())
            {
                Assert.Throws<XunitException>(
                    () => GitSnapshot.Verify(repository.SnapshotPath, "created\n"));

                repository.StageSnapshot();
                GitSnapshot.Verify(repository.SnapshotPath, "created\n");
            }
        }

        private sealed class TemporaryGitRepository : IDisposable
        {
            private readonly Repository repository;

            private TemporaryGitRepository(string path, Repository repository)
            {
                this.Path = path;
                this.repository = repository;
                this.SnapshotPath = System.IO.Path.Combine(path, "Snapshots", "Example.snap");
            }

            public string Path { get; }

            public string SnapshotPath { get; }

            public FileStatus Status => this.repository.RetrieveStatus("Snapshots/Example.snap");

            public static TemporaryGitRepository Create(string contents)
            {
                var repository = CreateWithoutSnapshot();
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(repository.SnapshotPath));
                File.WriteAllText(repository.SnapshotPath, contents);
                repository.StageSnapshot();
                repository.repository.Commit(
                    "Approve snapshot",
                    new Signature("PNFmt Tests", "tests@example.invalid", DateTimeOffset.UtcNow),
                    new Signature("PNFmt Tests", "tests@example.invalid", DateTimeOffset.UtcNow));
                return repository;
            }

            public static TemporaryGitRepository CreateWithoutSnapshot()
            {
                var path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "PNFmtGitSnapshotTests",
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(path);
                Repository.Init(path);
                return new TemporaryGitRepository(path, new Repository(path));
            }

            public void Dispose()
            {
                this.repository.Dispose();
                foreach (var file in Directory.GetFiles(this.Path, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(this.Path, true);
            }

            public void StageSnapshot()
            {
                Commands.Stage(this.repository, "Snapshots/Example.snap");
            }
        }
    }
}
