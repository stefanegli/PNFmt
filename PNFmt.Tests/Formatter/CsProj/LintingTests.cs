// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace PNFmt.Tests.Formatter.CsProj
{
    using PNFmt;

    using NFluent;

    using System;
    using System.IO;
    using System.Linq;

    using Xunit;

    public class LintingTests
    {
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

        private static System.Collections.Generic.IReadOnlyList<FormatterDiagnostic> Analyze(string project)
        {
            var tempFile = Path.Combine(
                Path.GetTempPath(),
                "CsProjFormatterTests",
                Guid.NewGuid().ToString("N") + ".csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(tempFile));
            File.WriteAllText(tempFile, project);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(tempFile), "Program.cs"), string.Empty);

            try
            {
                var formatter = new CsProjDocumentFormatter(
                    new DefaultCsProjFormatSettings(),
                    new FakeLog());
                formatter.RunWithResult(tempFile, writeChanges: false);
                return formatter.Diagnostics;
            }
            finally
            {
                File.Delete(tempFile);
                File.Delete(Path.Combine(Path.GetDirectoryName(tempFile), "Program.cs"));
            }
        }
    }
}
