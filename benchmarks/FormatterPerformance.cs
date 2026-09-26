using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PNFmt.Benchmarks
{
    internal static class FormatterPerformance
    {
        public static IEnumerable<PerformanceCaseResult> Run(string kind, string directory)
        {
            if (!new[] { "xml", "xaml", "csproj", "csharp", "resx" }.Contains(kind))
            {
                throw new ArgumentException("Unknown performance family: " + kind);
            }
            var count = kind == "csharp" ? 200 : 1000;
            var modes = kind == "csharp"
                ? new[] { "default", "width120", "wrap_if_long", "chop", "preferences", "header" }
                : kind == "resx" ? new[] { "lf", "crlf", "crlf-multiline", "crlf-long" }
                : new[] { "default", "width120", "chop" };
            foreach (var mode in modes)
            {
                var folder = Path.Combine(directory, mode);
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, "Input." + (kind == "csharp" ? "cs" : kind));
                File.WriteAllText(Path.Combine(folder, ".editorconfig"), Configuration(kind, mode));
                IFileFormatter formatter = kind switch
                {
                    "xml" => new XmlFormatter(),
                    "xaml" => new XamlFormatter(),
                    "csproj" => new CsProjFormatter(),
                    "resx" => new ResxFormatter(),
                    _ => new CSharpFormatter(),
                };
                var original = Encoding.UTF8.GetBytes(Input(kind, count, mode));
                File.WriteAllBytes(path, original);
                Validate(formatter.Format(new FileFormatRequest(path, true, false, new Log())), FileFormatStatus.Updated);
                var formatted = File.ReadAllBytes(path);
                Validate(formatter.Format(new FileFormatRequest(path, false, false, new Log())), FileFormatStatus.Unchanged);
                var hash = Convert.ToHexString(SHA256.HashData(formatted));
                foreach (var unchanged in new[] { false, true })
                {
                    var bytes = unchanged ? formatted : original;
                    File.WriteAllBytes(path, bytes);
                    var expected = unchanged ? FileFormatStatus.Unchanged : FileFormatStatus.Updated;
                    var request = new FileFormatRequest(path, false, false, new Log());
                    FileFormatResult Action() => formatter.Format(request);
                    var warm = Stopwatch.StartNew();
                    var warmCount = 0;
                    do
                    {
                        Validate(Action(), expected);
                        warmCount++;
                    }
                    while (warmCount < 10 || warm.ElapsedMilliseconds < 300);
                    var iterations = Math.Clamp((int)(100.0 / (warm.Elapsed.TotalMilliseconds / warmCount)), 1, 1000);
                    var measurement = new PerformanceCaseResult
                    {
                        Id = $"{kind}/{count}/{mode}/{(unchanged ? "unchanged" : "preview")}",
                        Milliseconds = new double[9],
                        AllocatedBytes = new double[9],
                        Iterations = iterations,
                        OutputHash = hash,
                    };
                    var results = new FileFormatResult[iterations];
                    var watch = new Stopwatch();
                    for (var sample = 0; sample < measurement.Milliseconds.Length; sample++)
                    {
                        var before = GC.GetTotalAllocatedBytes(precise: true);
                        watch.Restart();
                        for (var iteration = 0; iteration < iterations; iteration++)
                        {
                            results[iteration] = Action();
                        }
                        watch.Stop();
                        measurement.AllocatedBytes[sample] = (GC.GetTotalAllocatedBytes(precise: true) - before) / (double)iterations;
                        measurement.Milliseconds[sample] = watch.Elapsed.TotalMilliseconds / iterations;
                        foreach (var result in results)
                        {
                            Validate(result, expected);
                        }
                        if (!bytes.SequenceEqual(File.ReadAllBytes(path)))
                        {
                            throw new InvalidOperationException("Performance preview mutated its input.");
                        }
                    }
                    yield return measurement;
                }
            }
        }

        private static string Configuration(string kind, string mode)
        {
            return "root = true\n[*]\npnfmt_enabled = true\npnfmt_formatter = " + kind
                + "\npnfmt_format = true\npnfmt_sort_entries = true\nindent_style = space\nindent_size = 4\nend_of_line = "
                + (kind == "resx" && mode != "lf" ? "crlf" : "lf") + "\ninsert_final_newline = true\n"
                + (mode switch
                {
                    "width120" => "max_line_length = 120\nxml_wrap_tags_and_pi = true\n",
                    "wrap_if_long" => "max_line_length = 120\ncsharp_wrap_arguments_style = wrap_if_long\ncsharp_wrap_parameters_style = wrap_if_long\n",
                    "chop" when kind == "csharp" => "csharp_wrap_arguments_style = chop_always\ncsharp_wrap_parameters_style = chop_always\n",
                    "chop" => "xml_attribute_style = on_different_lines\n",
                    "preferences" => "dotnet_style_allow_multiple_blank_lines_experimental = false\ncsharp_style_allow_blank_lines_between_consecutive_braces_experimental = false\ndotnet_style_allow_statement_immediately_after_block_experimental = false\n",
                    "header" => "file_header_template = Copyright (c) Example.\\n{fileName}\n",
                    _ => "",
                });
        }

        private static string Input(string kind, int count, string mode)
        {
            var output = new StringBuilder();
            if (kind == "resx")
            {
                output.Append("<root><resheader name='resmimetype'><value>text/microsoft-resx</value></resheader>");
                var value = new string('x', mode == "crlf-long" ? 1000 : 100)
                    + (mode == "crlf-multiline" || mode == "crlf-long" ? "\r\nsecond&#xD;third" : "");
                for (var index = 0; index < count; index++)
                {
                    output.Append($"<data name='Key{index:D4}' xml:space='preserve'><value>{value}</value></data>");
                }
                return output.Append("</root>").ToString();
            }
            if (kind == "csharp")
            {
                output.Append("namespace Example { public class Worker {\n");
                for (var index = 0; index < count; index++)
                {
                    output.Append($"public string Build{index}(string firstParameter, string secondParameter, string thirdParameter, string fourthParameter, string fifthParameter, string sixthParameter){{\n\n\nreturn Compose(firstParameter, secondParameter, thirdParameter, fourthParameter, fifthParameter, sixthParameter);\n}}\n");
                }
                return output.Append("}}\n").ToString();
            }
            output.Append(kind == "csproj" ? "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup>"
                : kind == "xaml" ? "<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" : "<root>");
            for (var index = 0; index < count; index++)
            {
                output.Append(kind == "csproj"
                    ? $"<PackageReference Include=\"Company.Application.Component{index:D4}\" Version=\"1.2.3\" PrivateAssets=\"all\" IncludeAssets=\"runtime; build; native; contentfiles; analyzers; buildtransitive\"/>"
                    : kind == "xaml"
                        ? $"<Button Name=\"CommandButton{index:D4}\" Width=\"160\" Height=\"40\" Margin=\"8,4,8,4\" HorizontalAlignment=\"Stretch\" VerticalAlignment=\"Center\" Command=\"{{Binding ExecuteCommand}}\"/>"
                        : $"<entry name=\"Component{index:D4}\" source=\"some/long/path/to/the/input/data.json\" target=\"another/long/path/to/the/output/data.json\" enabled=\"true\" description=\"An example entry for wrapping measurements\"/>");
            }
            return output.Append(kind == "csproj" ? "</ItemGroup></Project>\n" : kind == "xaml" ? "</Grid>\n" : "</root>\n").ToString();
        }

        private static void Validate(FileFormatResult result, FileFormatStatus expected)
        {
            if (result.Status != expected || result.Diagnostics.Count != 0)
            {
                throw new InvalidOperationException($"Unexpected {result.Status}; expected {expected}; "
                    + string.Join(", ", result.Diagnostics.Select(item => item.Message)));
            }
        }

        private sealed class Log : IFormatterLog
        {
            public void Write(Exception exception) => throw new InvalidOperationException("Formatter error.", exception);
            public void WriteLine(string message) { }
        }
    }
}
