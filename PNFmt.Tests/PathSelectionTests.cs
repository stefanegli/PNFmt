using System;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using PNFmt.Cli;
using Xunit;

namespace PNFmt.Tests
{
    public sealed class PathSelectionTests
    {
        [Fact]
        public void Resolution_retains_case_distinct_files_and_deduplicates_repeated_targets()
        {
            using (var directory = new CaseDirectory())
            {
                File.WriteAllText(directory.Lower, "lower");
                var registry = new FormatterRegistry(new[] { new IniFormatter() });
                var resolver = new TargetFileResolver(registry, registry, new FilePatternMatcher(Array.Empty<string>()));
                var result = resolver.Resolve(new[] { directory.Path, directory.Upper, directory.Lower }, recursive: true);

                Assert.Empty(result.Errors);
                Assert.Equal(directory.IsCaseSensitive ? 2 : 1, result.Files.Count);
                if (directory.IsCaseSensitive)
                {
                    Assert.Contains(directory.Upper, result.Files);
                    Assert.Contains(directory.Lower, result.Files);
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Git_selection_distinguishes_clean_and_changed_case_variants(bool changeBoth)
        {
            using (var directory = new CaseDirectory())
            {
                Repository.Init(directory.Path);
                using (var repository = new Repository(directory.Path))
                {
                    Commands.Stage(repository, "A.ini");
                    var signature = new Signature("PNFmt Tests", "tests@example.invalid", DateTimeOffset.UtcNow);
                    repository.Commit("Initial", signature, signature);
                }

                File.WriteAllText(directory.Lower, "lower");
                if (changeBoth)
                {
                    File.AppendAllText(directory.Upper, "changed");
                }

                var context = GitRepositoryContext.Discover(directory.Path);

                Assert.True(context.IsChanged(directory.Lower));
                Assert.Equal(!directory.IsCaseSensitive || changeBoth, context.IsChanged(directory.Upper));
                Assert.Equal(directory.IsCaseSensitive && changeBoth ? 2 : 1,
                    context.GetChangedFiles(directory.Path, recursive: true).Count);

                var registry = new FormatterRegistry(new[] { new IniFormatter() });
                var result = new TargetFileResolver(registry, registry, new FilePatternMatcher(Array.Empty<string>()))
                    .Resolve(new[] { directory.Path }, recursive: true);
                Assert.Empty(result.Errors);
                Assert.Equal(context.GetChangedFiles(directory.Path, recursive: true).OrderBy(path => path),
                    result.Files.OrderBy(path => path));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Multiple_repositories_are_filtered_and_deduplicated_with_the_strictest_cpu_limit(bool parentScope)
        {
            using (var directory = new CaseDirectory())
            {
                var roots = new[] { Path.Combine(directory.Path, "first"), Path.Combine(directory.Path, "second") };
                var changed = roots.Select(root => Path.Combine(root, "Changed.ini")).ToArray();
                for (var index = 0; index < roots.Length; index++)
                {
                    Directory.CreateDirectory(roots[index]);
                    File.WriteAllText(Path.Combine(roots[index], ".pnfmt"), "{\"maxCpuCount\":" + (index + 2) + "}");
                    File.WriteAllText(Path.Combine(roots[index], "Clean.ini"), "clean");
                    Repository.Init(roots[index]);
                    using (var repository = new Repository(roots[index]))
                    {
                        Commands.Stage(repository, "*");
                        var signature = new Signature("PNFmt Tests", "tests@example.invalid", DateTimeOffset.UtcNow);
                        repository.Commit("Initial", signature, signature);
                    }

                    File.WriteAllText(changed[index], "changed");
                }

                File.Delete(directory.Upper);
                File.WriteAllText(Path.Combine(directory.Path, ".pnfmt"), "{\"maxCpuCount\":8}");
                var registry = new FormatterRegistry(new[] { new IniFormatter() });
                var targets = (parentScope ? new[] { directory.Path } : roots).Concat(changed).ToArray();
                var result = new TargetFileResolver(registry, registry, new FilePatternMatcher(Array.Empty<string>()))
                    .Resolve(targets, recursive: true);

                Assert.Empty(result.Errors);
                Assert.True(result.GitFiltered);
                Assert.Equal(changed, result.Files);
                Assert.Equal(2, result.MaxCpuCount);
            }
        }

        private sealed class CaseDirectory : IDisposable
        {
            private readonly TestDirectory directory = new TestDirectory();

            public CaseDirectory()
            {
                this.Upper = System.IO.Path.Combine(this.Path, "A.ini");
                this.Lower = System.IO.Path.Combine(this.Path, "a.ini");
                File.WriteAllText(this.Upper, "upper");
                this.IsCaseSensitive = !File.Exists(this.Lower);
            }

            public string Path => this.directory.Path;
            public string Upper { get; }
            public string Lower { get; }
            public bool IsCaseSensitive { get; }

            public void Dispose()
            {
                this.directory.Dispose();
            }
        }
    }
}
