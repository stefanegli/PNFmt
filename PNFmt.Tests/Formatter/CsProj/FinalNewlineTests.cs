// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using System.Text;
using Xunit;

namespace PNFmt.Tests.Formatter.CsProj
{
    public sealed class FinalNewlineTests
    {
        [Theory]
        [InlineData("lf", "\n", "true", true)]
        [InlineData("lf", "\n", "false", false)]
        [InlineData("lf", "\n", null, true)]
        [InlineData("crlf", "\r\n", "true", true)]
        [InlineData("crlf", "\r\n", "false", false)]
        [InlineData("crlf", "\r\n", null, true)]
        [InlineData("cr", "\r", "true", true)]
        [InlineData("cr", "\r", "false", false)]
        [InlineData("cr", "\r", null, true)]
        public void Formatting_honors_final_newline_policy_and_is_stable(
            string endOfLine,
            string newline,
            string setting,
            bool insertFinalNewline)
        {
            var directory = Path.Combine(Path.GetTempPath(), "PNFmtFinalNewlineTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var configuration = "root = true\n\n[*.csproj]\nend_of_line = " + endOfLine + "\n";
                if (setting is not null)
                {
                    configuration += "insert_final_newline = " + setting + "\n";
                }

                File.WriteAllText(Path.Combine(directory, ".editorconfig"), configuration);
                var path = Path.Combine(directory, "Project.csproj");
                var body = string.Join(newline,
                    "<Project Sdk=\"Microsoft.NET.Sdk\">",
                    "  <PropertyGroup>",
                    "    <TargetFramework>net10.0</TargetFramework>",
                    "  </PropertyGroup>",
                    "</Project>");
                var expected = Encoding.UTF8.GetBytes(body + (insertFinalNewline ? newline : string.Empty));
                var formatter = new CsProjFormatter();
                var preview = new FileFormatRequest(path, writeChanges: false, lint: false, NullFormatterLog.Instance);
                var write = new FileFormatRequest(path, writeChanges: true, lint: false, NullFormatterLog.Instance);

                foreach (var suffix in new[] { string.Empty, newline, newline + newline })
                {
                    var original = Encoding.UTF8.GetBytes(body + suffix);
                    File.WriteAllBytes(path, original);
                    var expectedStatus = suffix == (insertFinalNewline ? newline : string.Empty)
                        ? FileFormatStatus.Unchanged
                        : FileFormatStatus.Updated;

                    Assert.Equal(expectedStatus, formatter.Format(preview).Status);
                    Assert.Equal(original, File.ReadAllBytes(path));
                    Assert.Equal(expectedStatus, formatter.Format(write).Status);
                    Assert.Equal(expected, File.ReadAllBytes(path));
                    Assert.Equal(FileFormatStatus.Unchanged, formatter.Format(preview).Status);
                    Assert.Equal(FileFormatStatus.Unchanged, formatter.Format(write).Status);
                    Assert.Equal(expected, File.ReadAllBytes(path));
                }
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
