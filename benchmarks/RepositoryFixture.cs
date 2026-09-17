using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using PNFmt.Cli;

namespace PNFmt.Benchmarks
{
    internal sealed class RepositoryFixture
    {
        public static readonly string[] Extensions = { "csproj", "cs", "resx", "ini", "rsp", "slnx", "xml", "xaml" };
        private readonly Dictionary<string, byte[]> originals = new Dictionary<string, byte[]>();
        private Dictionary<string, byte[]> formattedBytes;

        public RepositoryFixture(string directory, int fileCount, bool mixed, bool nested)
        {
            this.DirectoryPath = directory;
            Directory.CreateDirectory(directory);
            var config = "root = true\n[*]\npnfmt_enabled = true\npnfmt_format = true\npnfmt_sort_entries = true\ncharset = utf-8\nindent_style = space\nindent_size = 4\nend_of_line = lf\n";
            foreach (var extension in Extensions)
            {
                config += "\n[*." + extension + "]\npnfmt_formatter = " + (extension == "cs" ? "csharp" : extension) + "\n";
            }

            File.WriteAllText(Path.Combine(directory, ".editorconfig"), config);
            for (var index = 0; index < fileCount; index++)
            {
                var parent = directory;
                if (nested)
                {
                    // Sixteen branches, each with four inherited overrides.
                    for (var depth = 0; depth < 4; depth++)
                    {
                        parent = Path.Combine(parent, depth == 0 ? "project-" + index % 16 : "level-" + depth);
                        if (!Directory.Exists(parent))
                        {
                            Directory.CreateDirectory(parent);
                            File.WriteAllText(Path.Combine(parent, ".editorconfig"),
                                "[*]\nindent_size = " + (depth + 2) + "\n[*.cs]\ncsharp_new_line_before_open_brace = all\n");
                        }
                    }
                }

                var extension = mixed ? Extensions[index % Extensions.Length] : "csproj";
                var path = Path.Combine(parent, "Input" + index.ToString("D5") + "." + extension);
                this.originals.Add(path, Encoding.UTF8.GetBytes(Input(extension, index)));
            }

            this.Files = this.originals.Keys.OrderBy(path => path, StringComparer.Ordinal).ToArray();
            this.Restore();
        }

        public string DirectoryPath { get; }
        public IReadOnlyList<string> Files { get; }

        public void Restore()
        {
            foreach (var file in this.originals)
            {
                File.WriteAllBytes(file.Key, file.Value);
            }
        }

        public void Validate(FormattingRunResult result, FileFormatStatus expected, bool formatted)
        {
            if (result.Outcomes.Count != this.Files.Count)
            {
                throw new InvalidOperationException("The runner did not process every fixture file.");
            }

            foreach (var outcome in result.Outcomes)
            {
                if (outcome.Error is not null || outcome.LoggedExceptions.Count != 0
                    || outcome.Result?.Status != expected || outcome.Result.Diagnostics.Count != 0)
                {
                    throw new InvalidOperationException($"Unexpected result for '{outcome.File}': {outcome.Result?.Status}; expected {expected}.", outcome.Error);
                }
            }

            var current = this.Files.ToDictionary(path => path, File.ReadAllBytes);
            if (formatted && this.formattedBytes is null)
            {
                this.formattedBytes = current;
            }

            var expectedBytes = formatted ? this.formattedBytes : this.originals;
            if (current.Any(file => !file.Value.SequenceEqual(expectedBytes[file.Key])))
            {
                throw new InvalidOperationException("Preview changed a file, or repeated/parallel formatting produced different bytes.");
            }

            if (Directory.EnumerateFiles(this.DirectoryPath, ".pnfmt-*", SearchOption.AllDirectories).Any())
            {
                throw new InvalidOperationException("Formatting left temporary files behind.");
            }
        }

        public void ValidateDiscovery(TargetFileResolution resolution)
        {
            if (resolution.Errors.Count != 0 || !resolution.Files.OrderBy(path => path, StringComparer.Ordinal).SequenceEqual(this.Files))
            {
                throw new InvalidOperationException("Discovery did not return exactly the fixture files: " + string.Join("; ", resolution.Errors));
            }
        }

        private static string Input(string extension, int index)
        {
            switch (extension)
            {
                case "csproj": return "<Project Sdk='Microsoft.NET.Sdk'><PropertyGroup><TargetFramework>net10.0</TargetFramework><Description>Project " + index + "</Description></PropertyGroup><ItemGroup><None Include='z.txt'/><None Include='a.txt'/></ItemGroup></Project>";
                case "cs": return "namespace Example{class C" + index + "{public int Add(int a,int b){return a+b;}}}";
                case "resx": return "<root><resheader name='resmimetype'><value>text/microsoft-resx</value></resheader><data name='z'><value>caf\u00e9</value></data><data name='a'><value>hello</value></data></root>";
                case "ini": return "[settings]\nz=2\na=1\n";
                case "rsp": return "z.cs\na.cs\n";
                case "slnx": return "<Solution><Project Path='Z.csproj'/><Project Path='A.csproj'/></Solution>";
                case "xml": return "<root><child name='first'>caf\u00e9</child><child name='second'/></root>";
                case "xaml": return "<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='hello'/></Grid>";
                default: throw new ArgumentOutOfRangeException(nameof(extension));
            }
        }
    }
}
