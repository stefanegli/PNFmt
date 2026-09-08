// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
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
                yield return new object[] { "Rsp", "Compiler.rsp", "rsp" };
                yield return new object[] { "Slnx", "Solution.slnx", "slnx" };
            }
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
            var formatter = CreateFormatter(formatterName);
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

        private static IFileFormatter CreateFormatter(string formatterName)
        {
            switch (formatterName)
            {
                case "csharp": return new CSharpFormatter();
                case "csproj": return new CsProjFormatter();
                case "ini": return new IniFormatter();
                case "rsp": return new RspFormatter();
                case "slnx": return new SlnxFormatter();
                default: throw new ArgumentOutOfRangeException(nameof(formatterName));
            }
        }
    }
}
