// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using PNFmt.Cli;
using Xunit;

namespace PNFmt.Tests
{
    public sealed class ProgramTests
    {
        [Fact]
        public void One_command_formats_csproj_and_resx_files()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                var projectPath = directory.Write(
                    "Project.csproj",
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"
                    + "<Zeta>z</Zeta><Alpha>a</Alpha></PropertyGroup></Project>");
                var resourcePath = directory.Write(
                    "Strings.resx",
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?><root>"
                    + "<resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>"
                    + "<data name=\"b\"><value>b</value></data>"
                    + "<data name=\"a\"><value>a</value></data></root>");
                var originalProject = File.ReadAllText(projectPath);
                var originalResource = File.ReadAllText(resourcePath);

                var checkResult = Run("--check", projectPath, resourcePath);

                Assert.Equal(1, checkResult.ExitCode);
                Assert.Contains("[would-update]", checkResult.Output);
                Assert.Equal(originalProject, File.ReadAllText(projectPath));
                Assert.Equal(originalResource, File.ReadAllText(resourcePath));

                var formatResult = Run(projectPath, resourcePath);

                Assert.Equal(0, formatResult.ExitCode);
                Assert.Contains("Updated 2", formatResult.Output);
                Assert.Contains("<Alpha>a</Alpha>", File.ReadAllText(projectPath));
                Assert.True(
                    File.ReadAllText(resourcePath).IndexOf("name=\"a\"", StringComparison.Ordinal)
                    < File.ReadAllText(resourcePath).IndexOf("name=\"b\"", StringComparison.Ordinal));

                var secondCheckResult = Run("--check", projectPath, resourcePath);

                Assert.Equal(0, secondCheckResult.ExitCode);
                Assert.Contains("unchanged 2", secondCheckResult.Output);
            }
        }

        [Fact]
        public void Recursive_directory_processing_uses_all_registered_extensions()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                var project = directory.Write(
                    Path.Combine("nested", "Project.csproj"),
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"
                    + "<Zeta>z</Zeta><Alpha>a</Alpha></PropertyGroup></Project>");
                var resource = directory.Write(
                    Path.Combine("nested", "Strings.resx"),
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?><root>"
                    + "<resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>"
                    + "<data name=\"b\"><value>b</value></data>"
                    + "<data name=\"a\"><value>a</value></data></root>");
                var solution = directory.Write(
                    Path.Combine("nested", "Solution.slnx"),
                    "<Solution><Project Path=\"Z.csproj\" />"
                    + "<Project Path=\"A.csproj\" /></Solution>");
                var ini = directory.Write(
                    Path.Combine("nested", "settings.ini"),
                    "z=2\r\na=1\r\n");

                var result = Run("--recursive", directory.Path);

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Updated 5", result.Output);
                Assert.True(
                    File.ReadAllText(project).IndexOf("<Alpha>", StringComparison.Ordinal)
                    < File.ReadAllText(project).IndexOf("<Zeta>", StringComparison.Ordinal));
                Assert.Equal(new[] { "a", "b" }, TemporaryDirectory.ReadNames(resource));
                Assert.True(
                    File.ReadAllText(solution).IndexOf("A.csproj", StringComparison.Ordinal)
                    < File.ReadAllText(solution).IndexOf("Z.csproj", StringComparison.Ordinal));
                Assert.Equal("a = 1\r\nz = 2\r\n", File.ReadAllText(ini));
            }
        }

        [Fact]
        public void Editorconfig_files_are_discovered_by_exact_file_name()
        {
            using (var directory = new TemporaryDirectory())
            {
                var editorConfig = directory.Write(
                    ".editorconfig",
                    "z=2\na=1\n");

                var result = Run(directory.Path);

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Updated 1", result.Output);
                Assert.Equal("a = 1\nz = 2\n", File.ReadAllText(editorConfig));
            }
        }

        [Fact]
        public void Unknown_options_and_unsupported_files_are_errors()
        {
            using (var directory = new TemporaryDirectory())
            {
                var unsupportedPath = directory.Write("notes.txt", "notes");

                var unknownOption = Run("--not-an-option");
                var unsupportedFile = Run(unsupportedPath);

                Assert.Equal(2, unknownOption.ExitCode);
                Assert.Contains("Unknown option", unknownOption.Error);
                Assert.Contains("Usage: pnfmt", unknownOption.Error);
                Assert.Equal(2, unsupportedFile.ExitCode);
                Assert.Contains("supported file type", unsupportedFile.Error);
            }
        }

        [Fact]
        public void Help_and_version_are_available()
        {
            var help = Run("--help");
            var version = Run("--version");

            Assert.Equal(0, help.ExitCode);
            Assert.Contains("Usage: pnfmt", help.Output);
            Assert.Equal(0, version.ExitCode);
            Assert.StartsWith("pnfmt ", version.Output);
        }

        [Fact]
        public void Invalid_path_syntax_is_reported_instead_of_crashing()
        {
            var result = Run("invalid\0path");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Unable to access path", result.Error);
            Assert.Contains("No supported files found.", result.Output);
        }

        [Fact]
        public void Path_errors_remain_errors_when_another_file_succeeds()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                var file = directory.WriteResx("valid.resx", "b", "a");
                var wrongExtension = directory.Write("not-supported.txt", "text");
                var missing = Path.Combine(directory.Path, "missing.resx");

                var result = Run(file, missing, wrongExtension);

                Assert.Equal(2, result.ExitCode);
                Assert.Contains("Path not found", result.Error);
                Assert.Contains("supported file type", result.Error);
                Assert.Equal(new[] { "a", "b" }, TemporaryDirectory.ReadNames(file));
            }
        }

        [Fact]
        public void Dry_run_succeeds_without_writing()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                var file = directory.WriteResx("dry.resx", "b", "a");
                var original = File.ReadAllText(file);

                var result = Run("--dry-run", file);

                Assert.Equal(0, result.ExitCode);
                Assert.Equal(original, File.ReadAllText(file));
                Assert.Contains("Would update 1", result.Output);
            }
        }

        [Fact]
        public void Duplicate_targets_are_processed_once()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                var file = directory.WriteResx("duplicate.resx", "b", "a");

                var result = Run(file, file);

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Processed 1 file(s)", result.Output);
            }
        }

        [Fact]
        public void Recursive_option_is_required_for_nested_files()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                var nestedFile = directory.WriteResx(Path.Combine("nested", "nested.resx"), "b", "a");
                var original = File.ReadAllText(nestedFile);

                var nonRecursive = Run(directory.Path);
                Assert.Equal(original, File.ReadAllText(nestedFile));
                var recursive = Run("--recursive", directory.Path);

                Assert.Equal(0, nonRecursive.ExitCode);
                Assert.Contains("Processed 1 file(s)", nonRecursive.Output);
                Assert.Equal(0, recursive.ExitCode);
                Assert.Equal(new[] { "a", "b" }, TemporaryDirectory.ReadNames(nestedFile));
            }
        }

        [Fact]
        public void Inactive_and_malformed_files_have_distinct_outcomes()
        {
            using (var inactiveDirectory = new TemporaryDirectory())
            using (var malformedDirectory = new TemporaryDirectory())
            {
                var inactive = inactiveDirectory.WriteResx("inactive.resx", "b", "a");
                var malformed = malformedDirectory.Write("malformed.resx", "<root>");
                malformedDirectory.EnableFormatting();

                var inactiveResult = Run(inactive);
                var malformedResult = Run("--verbose", malformed);

                Assert.Equal(0, inactiveResult.ExitCode);
                Assert.Contains("skipped", inactiveResult.Output);
                Assert.Equal(2, malformedResult.ExitCode);
                Assert.Contains("failed", malformedResult.Output);
                Assert.DoesNotContain("Processed", malformedResult.Output);
                Assert.Contains("System.Xml.XmlException", malformedResult.Error);
                Assert.Equal("<root>", File.ReadAllText(malformed));
            }
        }

        [Fact]
        public void Option_terminator_allows_a_dash_prefixed_file_name()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                var file = directory.WriteResx("-input.resx", "b", "a");
                var originalDirectory = Environment.CurrentDirectory;
                try
                {
                    Environment.CurrentDirectory = directory.Path;
                    var result = Run("--", "-input.resx");

                    Assert.Equal(0, result.ExitCode);
                    Assert.Equal(new[] { "a", "b" }, TemporaryDirectory.ReadNames(file));
                }
                finally
                {
                    Environment.CurrentDirectory = originalDirectory;
                }
            }
        }

        [Fact]
        public void Lint_reports_diagnostics_without_editorconfig()
        {
            using (var directory = new TemporaryDirectory())
            {
                var project = directory.Write(
                    "LintProject.csproj",
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"
                    + "<TargetFramework>net10.0</TargetFramework>"
                    + "<TargetFrameworks>net10.0;net9.0</TargetFrameworks>"
                    + "</PropertyGroup><ItemGroup /></Project>");

                var result = Run("--lint", project);

                Assert.Equal(1, result.ExitCode);
                Assert.Contains("warning CSPROJ001", result.Output);
                Assert.Contains("warning CSPROJ004", result.Output);
            }
        }

        [Fact]
        public void Recursive_processing_skips_generated_directories()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                directory.Write(
                    "Real.csproj",
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"
                    + "<Zeta>z</Zeta><Alpha>a</Alpha></PropertyGroup></Project>");
                var generated = directory.Write(
                    Path.Combine("bin", "Debug", "Generated.csproj"),
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"
                    + "<Zeta>z</Zeta><Alpha>a</Alpha></PropertyGroup></Project>");

                var result = Run("--recursive", directory.Path);

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Real.csproj", result.Output);
                Assert.DoesNotContain("Generated.csproj", result.Output);
                Assert.Contains("<Zeta>z</Zeta><Alpha>a</Alpha>", File.ReadAllText(generated));
            }
        }

        private static (int ExitCode, string Output, string Error) Run(params string[] args)
        {
            var originalOut = Console.Out;
            var originalError = Console.Error;
            using (var output = new StringWriter())
            using (var error = new StringWriter())
            {
                try
                {
                    Console.SetOut(output);
                    Console.SetError(error);
                    var exitCode = Program.Main(args);
                    return (exitCode, output.ToString(), error.ToString());
                }
                finally
                {
                    Console.SetOut(originalOut);
                    Console.SetError(originalError);
                }
            }
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                this.Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "PNFmtTests",
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(this.Path);
            }

            public string Path { get; }

            public void EnableFormatting()
            {
                this.Write(
                    ".editorconfig",
                    "root = true\r\n\r\n"
                    + "[*.csproj]\r\n"
                    + "csproj_formatter_sort_entries=true\r\n\r\n"
                    + "[*.resx]\r\n"
                    + "resx_formatter_sort_entries=true\r\n"
                    + "resx_formatter_remove_xsd_schema=true\r\n"
                    + "resx_formatter_remove_documentation_comment=true\r\n");
            }

            public static string[] ReadNames(string path)
            {
                var document = XDocument.Load(path);
                return document.Root
                    .Elements("data")
                    .Select(element => (string)element.Attribute("name"))
                    .ToArray();
            }

            public string WriteResx(string relativePath, params string[] names)
            {
                var entries = string.Join(
                    string.Empty,
                    names.Select(name => $"<data name=\"{name}\"><value>{name}</value></data>"));
                return this.Write(
                    relativePath,
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?><root>"
                    + "<resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>"
                    + entries
                    + "</root>");
            }

            public string Write(string relativePath, string contents)
            {
                var path = System.IO.Path.Combine(this.Path, relativePath);
                var parent = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                File.WriteAllText(path, contents);
                return path;
            }

            public void Dispose()
            {
                if (Directory.Exists(this.Path))
                {
                    Directory.Delete(this.Path, true);
                }
            }
        }
    }
}
