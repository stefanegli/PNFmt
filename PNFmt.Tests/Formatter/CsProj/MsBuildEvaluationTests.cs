using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace PNFmt.Tests.Formatter.CsProj
{
    public sealed class MsBuildEvaluationTests
    {
        [Fact]
        public async Task Repeated_assignments_preserve_values_observed_before_between_and_after_them()
        {
            var properties = "<Before>$(Zebra)</Before><Zebra>first</Zebra><Middle>$(Zebra)</Middle>"
                + "<Zebra>$(Zebra);second</Zebra><After>$(Zebra)</After>";
            var metadata = properties.Replace("$(", "%(");
            using (var project = new EvaluationProject("<PropertyGroup>" + properties + "</PropertyGroup>"
                + "<ItemGroup><None Include='file'>" + metadata + "</None></ItemGroup>"))
            {
                async Task<string[]> ReadValues()
                {
                    using (var document = JsonDocument.Parse(await project.EvaluateAsync(
                        "-getProperty:Before,Middle,After", "-getItem:None")))
                    {
                        var root = document.RootElement;
                        return new[] { root.GetProperty("Properties"), root.GetProperty("Items").GetProperty("None")[0] }
                            .SelectMany(element => new[] { "Before", "Middle", "After" }.Select(name => element.GetProperty(name).GetString()))
                            .ToArray();
                    }
                }

                var expected = new[] { "", "first", "first;second", "", "first", "first;second" };
                Assert.Equal(expected, await ReadValues());
                project.Format();
                Assert.Equal(expected, await ReadValues());
                project.AssertIdempotent();
            }
        }

        [Theory]
        [InlineData("$(File)")]
        [InlineData("*.txt")]
        [InlineData("?.txt")]
        [InlineData("a.txt;z.txt")]
        [InlineData("%7A.txt")]
        public async Task Sorting_preserves_the_first_expanded_duplicate_item(string include)
        {
            using (var project = new EvaluationProject("<PropertyGroup><File>z.txt</File></PropertyGroup>"
                + "<ItemGroup><None Include=\"z.txt\" Tag=\"first\"/><None Include=\"" + include + "\" Tag=\"second\"/></ItemGroup>"
                + "<Target Name=\"Audit\"><RemoveDuplicates Inputs=\"@(None)\">"
                + "<Output TaskParameter=\"Filtered\" ItemName=\"Unique\"/></RemoveDuplicates></Target>"))
            {
                project.WriteFile("z.txt", "fixture");
                async Task<string> ReadTag()
                {
                    using (var document = JsonDocument.Parse(await project.EvaluateAsync("-t:Audit", "-getItem:Unique")))
                    {
                        return document.RootElement.GetProperty("Items").GetProperty("Unique").EnumerateArray()
                            .Single(item => item.GetProperty("Identity").GetString() == "z.txt").GetProperty("Tag").GetString();
                    }
                }

                Assert.Equal("first", await ReadTag());
                project.Format();
                Assert.Equal("first", await ReadTag());
                project.AssertIdempotent();
            }
        }

        [Theory]
        [InlineData("$(Zebra)", "hello", false)]
        [InlineData("$(Zebra)", "", true)]
        [InlineData("$(Zebra.ToUpper())", "HELLO", false)]
        [InlineData("$([System.String]::Copy('$(Zebra)'))", "hello", false)]
        public async Task Sorting_preserves_references_in_structured_properties(string expression, string expected, bool forward)
        {
            var observed = "<Observed><Node Value=\"" + expression + "\" /></Observed>";
            const string Zebra = "<Zebra>hello</Zebra>";
            using (var project = new EvaluationProject("<PropertyGroup>"
                + (forward ? observed + Zebra : Zebra + observed) + "</PropertyGroup>"))
            {
                async Task<string> ReadValue() => (string)XElement.Parse(await project.EvaluateAsync("-getProperty:Observed")).Attribute("Value");

                Assert.Equal(expected, await ReadValue());
                project.Format();
                Assert.Equal(expected, await ReadValue());
                project.AssertIdempotent();
            }
        }

        [Theory]
        [InlineData(false, "hello")]
        [InlineData(true, "")]
        public async Task Sorting_preserves_references_in_structured_metadata(bool forward, string expected)
        {
            const string Alpha = "<Alpha><Node Value=\"%(Zebra)\" /></Alpha>";
            const string Zebra = "<Zebra>hello</Zebra>";
            using (var project = new EvaluationProject("<ItemGroup><None Include=\"file\">"
                + (forward ? Alpha + Zebra : Zebra + Alpha) + "</None></ItemGroup>"))
            {
                async Task<string> ReadValue()
                {
                    using (var document = JsonDocument.Parse(await project.EvaluateAsync("-getItem:None")))
                    {
                        var xml = document.RootElement.GetProperty("Items").GetProperty("None")[0].GetProperty("Alpha").GetString();
                        return (string)XElement.Parse(xml).Attribute("Value");
                    }
                }

                Assert.Equal(expected, await ReadValue());
                project.Format();
                Assert.Equal(expected, await ReadValue());
                project.AssertIdempotent();
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("end_of_line = lf\n")]
        [InlineData("end_of_line = crlf\n")]
        [InlineData("end_of_line = cr\n")]
        public async Task Formatting_preserves_property_and_metadata_newlines(string settings)
        {
            const string Value = "first&#xD;second&#xD;&#xA;third&#xA;fourth";
            const string Expected = "first\rsecond\r\nthird\nfourth";
            using (var project = new EvaluationProject(
                "<PropertyGroup><Observed>" + Value + "</Observed></PropertyGroup>"
                + "<ItemGroup><None Include=\"file\"><Observed>" + Value + "</Observed></None></ItemGroup>", settings))
            {
                async Task AssertValues()
                {
                    using (var properties = JsonDocument.Parse(await project.EvaluateAsync("-getProperty:Observed,EnableDefaultItems")))
                    using (var items = JsonDocument.Parse(await project.EvaluateAsync("-getItem:None")))
                    {
                        Assert.Equal(Expected, properties.RootElement.GetProperty("Properties").GetProperty("Observed").GetString());
                        Assert.Equal(Expected, items.RootElement.GetProperty("Items").GetProperty("None")[0].GetProperty("Observed").GetString());
                    }
                }

                await AssertValues();
                project.Format();
                await AssertValues();
                project.AssertIdempotent();
            }
        }

        [Theory]
        [InlineData("<None Include=\"file\" Zebra=\"hello\" Alpha=\"%(Zebra)\" />", "Alpha", "hello")]
        [InlineData("<None Include=\"file\" Alpha=\"%(Zebra)\" Zebra=\"hello\" />", "Alpha", "")]
        [InlineData("<None Include=\"file\"><Zebra>hello</Zebra><Alpha>%(Zebra)</Alpha></None>", "Alpha", "hello")]
        [InlineData("<None Include=\"file\"><Alpha>%(Zebra)</Alpha><Zebra>hello</Zebra></None>", "Alpha", "")]
        [InlineData("<None Include=\"file\"><Zebra>first</Zebra><Alpha>%(Zebra)</Alpha><Zebra>second</Zebra></None>", "Alpha", "first")]
        [InlineData("<None Include=\"file\" LinkBase=\"assets\" Link=\"%(LinkBase)/%(Filename)%(Extension)\" />", "Link", "assets/file")]
        public async Task Sorting_preserves_metadata_evaluation(string item, string name, string expected)
        {
            using (var project = new EvaluationProject("<ItemGroup>" + item + "</ItemGroup>"))
            {
                async Task<string> ReadMetadata()
                {
                    using (var document = JsonDocument.Parse(await project.EvaluateAsync("-getItem:None")))
                    {
                        return document.RootElement.GetProperty("Items").GetProperty("None")[0].GetProperty(name).GetString();
                    }
                }

                Assert.Equal(expected, await ReadMetadata());
                project.Format();
                Assert.Equal(expected, await ReadMetadata());
                project.AssertIdempotent();
            }
        }

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

        [Theory]
        [InlineData("<None Include=\"a\"><Prior>@(None)</Prior></None>", "a=z")]
        [InlineData("<None Include=\"a\" Prior=\"@(None)\" />", "a=z")]
        [InlineData("<None Include=\"a\" Condition=\"'@(None)' != ''\" />", "a=")]
        public async Task Sorting_preserves_item_references_outside_include(string item, string expected)
        {
            using (var project = new EvaluationProject("<ItemGroup><None Include=\"z\" />" + item + "</ItemGroup>"))
            {
                async Task<string[]> ReadItems()
                {
                    using (var document = JsonDocument.Parse(await project.EvaluateAsync("-getItem:None")))
                    {
                        return document.RootElement.GetProperty("Items").GetProperty("None").EnumerateArray()
                            .Select(value => value.GetProperty("Identity").GetString() + "="
                                + (value.TryGetProperty("Prior", out var prior) ? prior.GetString() : string.Empty))
                            .ToArray();
                    }
                }

                var before = await ReadItems();
                Assert.Equal(new[] { "z=", expected }, before);

                project.Format();

                Assert.Equal(before, await ReadItems());
                project.AssertIdempotent();
            }
        }

        private sealed class EvaluationProject : IDisposable
        {
            private readonly string directory = Path.Combine(Path.GetTempPath(), "PNFmtEvaluationTests", Guid.NewGuid().ToString("N"));
            private readonly string path;

            public EvaluationProject(string body, string settings = "")
            {
                Directory.CreateDirectory(this.directory);
                File.WriteAllText(Path.Combine(this.directory, ".editorconfig"),
                    "root = true\n[*.csproj]\npnfmt_sort_entries = true\n" + settings);
                this.path = Path.Combine(this.directory, "Evaluation.csproj");
                File.WriteAllText(this.path, "<Project Sdk=\"Microsoft.NET.Sdk\">"
                    + "<PropertyGroup><EnableDefaultItems>false</EnableDefaultItems></PropertyGroup>"
                    + body + "</Project>");
            }

            public void WriteFile(string name, string contents) => File.WriteAllText(Path.Combine(this.directory, name), contents);

            public async Task<string> EvaluateAsync(params string[] queries)
            {
                var start = new ProcessStartInfo("dotnet")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = this.directory,
                };
                foreach (var argument in new[] { "msbuild", this.path, "-nologo" }.Concat(queries))
                {
                    start.ArgumentList.Add(argument);
                }

                using (var process = Process.Start(start))
                {
                    var result = await TestProcess.ReadAsync(process);
                    Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
                    return result.StandardOutput.Trim();
                }
            }

            public void Format()
            {
                new CsProjFormatter().Format(new FileFormatRequest(this.path, true, false, NullFormatterLog.Instance));
            }

            public void AssertIdempotent()
            {
                var bytes = File.ReadAllBytes(this.path);
                var result = new CsProjFormatter().Format(new FileFormatRequest(this.path, true, false, NullFormatterLog.Instance));
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
