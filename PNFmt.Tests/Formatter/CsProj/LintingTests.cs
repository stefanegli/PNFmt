// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace PNFmt.Tests.Formatter.CsProj
{
    using System;
    using System.IO;
    using System.Linq;

    using NFluent;

    using PNFmt;

    using Xunit;

    public class LintingTests
    {
        [Theory]
        [InlineData("Compile", "Missing*.cs", "Existing.cs", false)]
        [InlineData("Compile", "Existing*.cs", "Existing.cs", true)]
        [InlineData("Compile", "*.cs", "nested/Existing.cs", false)]
        [InlineData("Compile", "**/*.cs", "Existing.cs", true)]
        [InlineData("Compile", "**/*.cs", "nested/Existing.cs", true)]
        [InlineData("Compile", "**/Missing*.cs", "nested/Existing.cs", false)]
        [InlineData("Compile", "src/*/Match?.cs", "src/child/Match1.cs", true)]
        [InlineData("Compile", "src/*/Match?.cs", "src/child/deep/Match1.cs", false)]
        [InlineData("Compile", "src/*/Match?.cs", "src/child/Match10.cs", false)]
        [InlineData("Compile", @"src\**\Match?.cs", "src/child/Match1.cs", true)]
        [InlineData("Compile", @"src\Missing*.cs", "src/Existing.cs", false)]
        [InlineData("Compile", "./src/*.cs", "src/Existing.cs", true)]
        [InlineData("EmbeddedResource", "Missing*.resx", "Existing.resx", false)]
        [InlineData("EmbeddedResource", @"src\*.resx", "src/Existing.resx", true)]
        [InlineData("None", @"src\data.txt", "src/data.txt", true)]
        public void Default_item_lint_requires_a_file_matching_the_entire_include(string itemType, string include, string file, bool expectedDiagnostic)
        {
            var project = "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><" + itemType
                + " Include=\"" + include + "\" /></ItemGroup></Project>";

            var diagnostics = Analyze(project, true, file);

            Assert.Equal(expectedDiagnostic, diagnostics.Any(diagnostic => diagnostic.Code == "CSPROJ005"));
        }

        [Fact]
        public void Formatting_does_not_run_project_lints()
        {
            const string Project =
                "<Project Sdk=\"Microsoft.NET.Sdk\">"
                + "<PropertyGroup>"
                + "<TargetFramework>net10.0</TargetFramework>"
                + "<TargetFrameworks>net10.0;net9.0</TargetFrameworks>"
                + "</PropertyGroup>"
                + "</Project>";

            var diagnostics = Analyze(Project, lint: false);

            Assert.Empty(diagnostics);
        }

        [Fact]
        public void Lint_reports_structural_project_issues()
        {
            var project = string.Join(
                "\n",
                "<Project Sdk=\"Microsoft.NET.Sdk\">",
                "  <PropertyGroup>",
                "    <TargetFramework>net10.0</TargetFramework>",
                "    <TargetFrameworks>net10.0;net9.0</TargetFrameworks>",
                "  </PropertyGroup>",
                "  <PropertyGroup />",
                "  <ItemGroup />",
                "  <ItemGroup>",
                "    <PackageReference Include=\"Same.Package\" Version=\"1.0.0\" />",
                "    <PackageReference Include=\"Same.Package\" Version=\"1.0.0\" />",
                "    <ProjectReference Include=\"Other.csproj\" />",
                "  </ItemGroup>",
                "  <ItemGroup>",
                "    <Compile Include=\"Program.cs\" />",
                "  </ItemGroup>",
                "  <When Condition=\"'$(Configuration)' == 'Debug'\" />",
                "</Project>");

            var diagnostics = Analyze(project);
            var codes = diagnostics.Select(diagnostic => diagnostic.Code).ToList();

            Check.That(codes).Contains("CSPROJ001");
            Check.That(codes).Contains("CSPROJ002");
            Check.That(codes).Contains("CSPROJ003");
            Check.That(codes).Contains("CSPROJ004");
            Check.That(codes).Contains("CSPROJ005");
            Check.That(codes).Contains("CSPROJ006");
            Check.That(diagnostics.All(diagnostic => diagnostic.LineNumber.HasValue)).IsTrue();
        }

        [Fact]
        public void Lint_respects_disabled_default_items()
        {
            var project = string.Join(
                "\n",
                "<Project Sdk=\"Microsoft.NET.Sdk\">",
                "  <PropertyGroup>",
                "    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>",
                "    <TargetFramework>net10.0</TargetFramework>",
                "  </PropertyGroup>",
                "  <ItemGroup>",
                "    <Compile Include=\"Program.cs\" />",
                "  </ItemGroup>",
                "</Project>");

            var diagnostics = Analyze(project);

            Check.That(diagnostics.Select(diagnostic => diagnostic.Code)).Not.Contains("CSPROJ005");
        }

        [Theory]
        [InlineData("")]
        [InlineData("http://schemas.microsoft.com/developer/msbuild/2003")]
        public void Non_sdk_projects_receive_structural_lints_without_sdk_default_item_warnings(string xmlNamespace)
        {
            var project = "<Project xmlns=\"" + xmlNamespace + "\"><PropertyGroup/>"
                + "<ItemGroup><Compile Include=\"Program.cs\"/></ItemGroup></Project>";

            var diagnostic = Assert.Single(Analyze(project));

            Assert.Equal("CSPROJ001", diagnostic.Code);
        }

        private static System.Collections.Generic.IReadOnlyList<FormatterDiagnostic> Analyze(
            string project,
            bool lint = true,
            params string[] files)
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "CsProjFormatterTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var tempFile = Path.Combine(directory, "Test.csproj");
            File.WriteAllText(tempFile, project);
            File.WriteAllText(Path.Combine(directory, ".editorconfig"),
                "root = true\n[*]\npnfmt_enabled = true\npnfmt_formatter = csproj\n");
            foreach (var file in files.Length == 0 ? new[] { "Program.cs" } : files)
            {
                var path = Path.Combine(directory, file.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, string.Empty);
            }

            try
            {
                var result = new CsProjFormatter().Format(
                    new FileFormatRequest(tempFile, writeChanges: false, lint: lint, NullFormatterLog.Instance));
                Assert.Equal(project, File.ReadAllText(tempFile));
                return result.Diagnostics;
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
