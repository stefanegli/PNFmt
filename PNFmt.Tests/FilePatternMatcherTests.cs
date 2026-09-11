using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PNFmt.Cli;
using PNFmt.Tests.Snapshots;
using Xunit;

namespace PNFmt.Tests
{
    public sealed class FilePatternMatcherTests
    {
        [Fact]
        public async Task Oversized_globs_are_usage_errors_instead_of_process_crashes()
        {
            var pattern = string.Concat(Enumerable.Repeat("a?", 1000)) + ".cs";
            var result = await CliTestRunner.RunCliAsync(Path.GetTempPath(),
                "--all", "--check", "--file-pattern", pattern, Path.GetTempPath());
            Assert.Equal(2, result.ExitCode);
            Assert.Contains("File pattern is too complex", result.StandardError);
            Assert.DoesNotContain("Unhandled exception", result.StandardError);
            Assert.DoesNotContain("Processed", result.StandardOutput);
        }

        [Theory]
        [InlineData("src/*.cs", "src/File.cs", true)]
        [InlineData("src/*.cs", "src/nested/File.cs", false)]
        [InlineData("src/**/*.cs", "src/File.cs", true)]
        [InlineData("src/**/*.cs", "src/nested/File.cs", true)]
        [InlineData("src/?.cs", "src/A.cs", true)]
        [InlineData("src/?.cs", "src/AB.cs", false)]
        [InlineData("src/[A].cs", "src/[A].cs", true)]
        [InlineData("./src\\*.CS", "src/File.cs", true)]
        public void Patterns_preserve_segment_glob_and_literal_behavior(string pattern, string relativePath, bool expected)
        {
            var root = Path.Combine(Path.GetTempPath(), "PNFmtGlobTests");
            var matcher = new FilePatternMatcher(new[] { pattern });
            Assert.Equal(expected, matcher.IsMatch(Path.Combine(root, relativePath), root));
        }

        [Fact]
        public async Task Repeated_wildcards_finish_without_exponential_backtracking()
        {
            var directory = Path.Combine(Path.GetTempPath(), "PNFmtGlobTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(Path.Combine(directory, new string('a', 34) + ".cs"), "class C {}");
                var pattern = string.Concat(Enumerable.Repeat("*a", 28)) + "b.cs";
                var result = await CliTestRunner.RunCliAsync(directory, "--all", "--file-pattern", pattern, directory);
                Assert.Equal(0, result.ExitCode);
                Assert.Contains("No supported files found", result.StandardOutput);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
