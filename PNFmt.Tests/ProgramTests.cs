// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
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
                directory.Write(
                    Path.Combine("nested", "Project.csproj"),
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"
                    + "<Zeta>z</Zeta><Alpha>a</Alpha></PropertyGroup></Project>");
                directory.Write(
                    Path.Combine("nested", "Strings.resx"),
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?><root>"
                    + "<resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>"
                    + "<data name=\"b\"><value>b</value></data>"
                    + "<data name=\"a\"><value>a</value></data></root>");

                var result = Run("--recursive", directory.Path);

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Updated 2", result.Output);
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

