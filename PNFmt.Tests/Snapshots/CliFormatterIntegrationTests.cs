// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PNFmt.Tests.Snapshots
{
    public sealed class CliFormatterIntegrationTests
    {
        public static IEnumerable<object[]> Cases
        {
            get
            {
                yield return new object[] { "CSharp", "Source.cs", "csharp" };
                yield return new object[] { "CSharp", "Cleanup.cs", "csharp" };
                yield return new object[] { "CsProj", "SimpleSort.csproj", "csproj" };
                yield return new object[] { "Ini", "PrefixGroups.ini", "ini" };
                yield return new object[] { "Resx", "Strings.resx", "resx" };
                yield return new object[] { "Rsp", "Compiler.rsp", "rsp" };
                yield return new object[] { "Slnx", "Solution.slnx", "slnx" };
                yield return new object[] { "Xml", "Data.xml", "xml" };
                yield return new object[] { "Xml", "View.xaml", "xaml" };
            }
        }

        [Fact]
        public void Every_registered_formatter_has_a_cli_fixture()
        {
            var registered = FormatterCatalog.CreateDefault().Formatters.Select(formatter => formatter.Name);
            var covered = Cases.Select(testCase => (string)testCase[2]).Distinct(StringComparer.Ordinal);
            Assert.Equal(registered.OrderBy(name => name, StringComparer.Ordinal),
                covered.OrderBy(name => name, StringComparer.Ordinal));
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public async Task Cli_matches_the_formatter(
            string formatterDirectory,
            string relativePath,
            string formatterName)
        {
            var fixtureRoot = Path.Combine(
                AppContext.BaseDirectory,
                "Formatter",
                formatterDirectory,
                "_files");
            var inputFile = Path.Combine(fixtureRoot, "input", relativePath);
            var formatter = FormatterCatalog.CreateDefault().Formatters.Single(item => item.Name == formatterName);
            var expected = FormatterSnapshotTestRunner.FormatAndAssertIdempotent(
                formatter,
                fixtureRoot,
                relativePath,
                inputFile,
                relativePath);

            var actual = await CliTestRunner.RunAndAssertAsync(
                fixtureRoot,
                relativePath,
                inputFile,
                relativePath);

            Assert.Equal(expected, actual);
        }

    }
}
