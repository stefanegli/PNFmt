using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace PNFmt.Tests.Formatter.CsProj
{
    public sealed class MsBuildEvaluationTests
    {
        [Theory]
        [InlineData("$(Zebra.ToUpper())", "HELLO")]
        [InlineData("$(Zebra.Length)", "5")]
        [InlineData("$([System.String]::Copy('$(Zebra)'))", "hello")]
        public async Task Sorting_preserves_property_function_evaluation(string expression, string expected)
        {
            using (var project = new EvaluationProject(
                "<PropertyGroup><Zebra>hello</Zebra><Observed>" + expression
                + "</Observed></PropertyGroup>"))
            {
                Assert.Equal(expected, await project.EvaluateAsync("-getProperty:Observed"));

                project.Format();

                Assert.Equal(expected, await project.EvaluateAsync("-getProperty:Observed"));
                project.AssertIdempotent();
            }
        }

        [Fact]
        public async Task Sorting_preserves_property_functions_in_conditions()
        {
            using (var project = new EvaluationProject(
                "<PropertyGroup><Zebra>hello</Zebra>"
                + "<Observed Condition=\"'$(Zebra.ToUpper())' == 'HELLO'\">matched</Observed>"
                + "</PropertyGroup>"))
            {
                Assert.Equal("matched", await project.EvaluateAsync("-getProperty:Observed"));

                project.Format();

                Assert.Equal("matched", await project.EvaluateAsync("-getProperty:Observed"));
                project.AssertIdempotent();
            }
        }

        private sealed class EvaluationProject : IDisposable
        {
            private readonly string directory = Path.Combine(Path.GetTempPath(), "PNFmtEvaluationTests", Guid.NewGuid().ToString("N"));
            private readonly string path;

            public EvaluationProject(string body)
            {
                Directory.CreateDirectory(this.directory);
                File.WriteAllText(Path.Combine(this.directory, ".editorconfig"),
                    "root = true\n[*.csproj]\npnfmt_sort_entries = true\n");
                this.path = Path.Combine(this.directory, "Evaluation.csproj");
                File.WriteAllText(this.path, "<Project Sdk=\"Microsoft.NET.Sdk\">"
                    + "<PropertyGroup><EnableDefaultItems>false</EnableDefaultItems></PropertyGroup>"
                    + body + "</Project>");
            }

            public async Task<string> EvaluateAsync(string query)
            {
                var start = new ProcessStartInfo("dotnet")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = this.directory,
                };
                foreach (var argument in new[] { "msbuild", this.path, "-nologo", query })
                {
                    start.ArgumentList.Add(argument);
                }

                using (var process = Process.Start(start))
                {
                    var output = process.StandardOutput.ReadToEndAsync();
                    var error = process.StandardError.ReadToEndAsync();
                    try
                    {
                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                    }
                    finally
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                        }
                    }

                    Assert.True(process.ExitCode == 0, await error);
                    return (await output).Trim();
                }
            }

            public void Format()
            {
                new CsProjFormatter().Format(new FileFormatRequest(this.path, true, false, new FakeLog()));
            }

            public void AssertIdempotent()
            {
                var bytes = File.ReadAllBytes(this.path);
                var result = new CsProjFormatter().Format(new FileFormatRequest(this.path, true, false, new FakeLog()));
                Assert.Equal(FileFormatStatus.Unchanged, result.Status);
                Assert.Equal(bytes, File.ReadAllBytes(this.path));
            }

            public void Dispose()
            {
                Directory.Delete(this.directory, recursive: true);
            }
        }
    }
}
