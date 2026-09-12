// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using Xunit;

namespace PNFmt.Tests.Snapshots
{
    // Other snapshot tests write the files this catalog reads.
    [CollectionDefinition("Snapshot review", DisableParallelization = true)]
    public sealed class SnapshotReviewCollection { }

    [Collection("Snapshot review")]
    public sealed class SnapshotReviewExporterTests
    {
        [Fact]
        public void Every_saved_snapshot_has_an_input_and_result_in_the_review_catalog()
        {
            using (var repository = new Repository(Repository.Discover(AppContext.BaseDirectory)))
            {
                var root = repository.Info.WorkingDirectory;
                var cases = SnapshotReviewExporter.ReadCases(root);
                var saved = Directory.GetFiles(Path.Combine(root, "Snapshots"), "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).OrderBy(path => path);
                Assert.NotEmpty(cases);
                Assert.Equal(saved, cases.Select(item => item.id).OrderBy(path => path));
                Assert.Equal(8, cases.Select(item => item.provider).Distinct().Count());
                Assert.All(cases, item => Assert.Equal(File.ReadAllText(Path.Combine(root, item.id)), item.result));
                Assert.StartsWith("root=true\n\n[*.resx]\nindent_size: 2", cases.Single(item => item.name == "ColonAssignments.editorconfig").input);
                Assert.EndsWith("Plain.xml", cases.Single(item => item.name == "Plain.resx").inputPath);
                Assert.Empty(cases.Single(item => item.name == "Default.editorconfig").input);
                var inherited = cases.Single(item => item.name == "config1/Sort.resx");
                Assert.Contains("_editor/.editorconfig", inherited.settings);
                Assert.Contains("_editor/config1/.editorconfig", inherited.settings);
            }
        }

        [Fact]
        public void Export_embeds_snapshot_text_as_json_without_executable_markup()
        {
            using (var repository = new Repository(Repository.Discover(AppContext.BaseDirectory)))
            using (var directory = new TestDirectory())
            {
                var output = directory.GetPath("review.html");
                SnapshotReviewExporter.Write(repository.Info.WorkingDirectory, output);
                var html = File.ReadAllText(output);
                Assert.DoesNotContain("/*SNAPSHOT_DATA*/", html);
                Assert.Contains("\\u003C", html);
                Assert.Contains("ColonAssignments.editorconfig", html);
            }
        }
    }
}
