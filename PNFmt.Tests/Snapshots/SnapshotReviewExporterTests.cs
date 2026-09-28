// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;

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

        [Fact]
        public void Wrapping_review_separates_processed_pending_and_reference_cases_and_retains_before_results()
        {
            using (var repository = new Repository(Repository.Discover(AppContext.BaseDirectory)))
            using (var directory = new TestDirectory())
            {
                var root = repository.Info.WorkingDirectory;
                var cases = SnapshotReviewExporter.ReadCases(root);
                var review = cases.Where(item => item.review is not null).ToArray();
                Assert.Equal(9, review.Count(item => item.review.status == "processed"));
                Assert.Equal(10, review.Count(item => item.review.status == "pending"));
                Assert.Equal(4, review.Count(item => item.review.status == "reference"));
                Assert.All(cases.Where(item => item.name.StartsWith("WrappingReview/", StringComparison.Ordinal)),
                    item => Assert.NotNull(item.review));
                var previous = review.Where(item => item.review.before is not null).ToArray();
                Assert.Equal(new[] { "WrappingReview/ArrayInitializers.cs", "WrappingReview/CollectionExpressions.cs", "WrappingReview/ConditionalExpressions.cs" },
                    previous.Select(item => item.name).ToArray());
                Assert.All(previous, item =>
                {
                    Assert.Equal(item.input, item.review.before);
                    Assert.NotEqual(item.result, item.review.before);
                });
                Assert.Equal("pending", review.Single(item => item.name == "WrappingReview/SwitchExpressions.cs").review.status);
                var output = directory.GetPath("wrapping.html");
                SnapshotReviewExporter.Write(root, output, wrappingReviewOnly: true);
                var dataLine = File.ReadLines(output).Single(line => line.TrimStart().StartsWith("const data = ", StringComparison.Ordinal));
                using (var json = JsonDocument.Parse(dataLine.Trim().Substring("const data = ".Length).TrimEnd(';')))
                {
                    Assert.Equal(review.Select(item => item.id), json.RootElement.GetProperty("cases").EnumerateArray()
                        .Select(item => item.GetProperty("id").GetString()));
                }
            }
        }
    }
}
