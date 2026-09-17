// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xunit;

namespace PNFmt.Tests.Formatter
{
    public sealed class FormatterContractTests
    {
        // Null means that the existing final-newline presence is retained.
        public static IEnumerable<object[]> FinalNewlineCases
        {
            get
            {
                yield return new object[] { "csharp", "Sample.cs", "class C {}", "pnfmt_csharp_format = true\n", null, null };
                yield return new object[] { "csproj", "Sample.csproj", "<Project Sdk='Microsoft.NET.Sdk' />", "pnfmt_sort_entries = true\n", false, true };
                yield return new object[] { "ini", "Settings.ini", "a = 1", "pnfmt_sort_entries = true\n", true, true };
                yield return new object[] { "resx", "Strings.resx",
                    "<root><resheader name='resmimetype'><value>text/microsoft-resx</value></resheader></root>",
                    "pnfmt_sort_entries = true\npnfmt_resx_remove_documentation_comment = true\npnfmt_resx_remove_xsd_schema = true\n", false, false };
                yield return new object[] { "rsp", "Compiler.rsp", "a.cs", "pnfmt_sort_entries = true\n", true, true };
                yield return new object[] { "slnx", "Solution.slnx", "<Solution />", "pnfmt_sort_entries = true\n", true, true };
                yield return new object[] { "xml", "Data.xml", "<root />", "pnfmt_xml_format = true\n", null, null };
                yield return new object[] { "xaml", "View.xaml", "<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' />",
                    "pnfmt_xaml_format = true\n", null, null };
            }
        }

        public static IEnumerable<object[]> ActivationCases => FinalNewlineCases.Select(row => row.Take(3).ToArray());

        [Theory]
        [MemberData(nameof(FinalNewlineCases))]
        public void Final_newline_behavior_remains_compatible(
            string name, string fileName, string input, string activation, bool? whenFalse, bool? whenMissing)
        {
            using (var directory = new TestDirectory())
            {
                var formatter = FormatterCatalog.CreateDefault().Formatters.Single(item => item.Name == name);
                foreach (var setting in new[] { "true", "false", null })
                {
                    directory.Write(".editorconfig", "root = true\n[*]\nend_of_line = lf\nindent_size = 2\n" + activation
                        + (setting is null ? "" : "insert_final_newline = " + setting + "\n"));
                    foreach (var hasNewline in new[] { false, true })
                    {
                        var path = directory.Write(fileName, input + (hasNewline ? "\n" : ""));
                        var expected = setting == "true" || ((setting == "false" ? whenFalse : whenMissing) ?? hasNewline);
                        var original = File.ReadAllBytes(path);
                        var preview = formatter.Format(new FileFormatRequest(path, false, false, NullFormatterLog.Instance));
                        Assert.Equal(original, File.ReadAllBytes(path));
                        var result = formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance));
                        Assert.Equal(preview.Status, result.Status);
                        Assert.NotEqual(FileFormatStatus.Skipped, result.Status);
                        Assert.Empty(result.Diagnostics);
                        Assert.Equal(expected, File.ReadAllText(path).EndsWith("\n", StringComparison.Ordinal));
                        Assert.Equal(FileFormatStatus.Unchanged,
                            formatter.Format(new FileFormatRequest(path, false, false, NullFormatterLog.Instance)).Status);
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(ActivationCases))]
        public void Shared_settings_follow_the_documented_activation_rules(string name, string fileName, string input)
        {
            using (var directory = new TestDirectory())
            {
                var path = directory.Write(fileName, input);
                var formatter = FormatterCatalog.CreateDefault().Formatters.Single(item => item.Name == name);
                foreach (var setting in new[] { "indent_size = 2", "end_of_line = lf", "insert_final_newline = true", "pnfmt_sort_entries = false" })
                {
                    directory.Write(".editorconfig", "root = true\n[*]\n" + setting + "\n");
                    var active = name == "csproj" || (name == "resx" && setting == "pnfmt_sort_entries = false");
                    var result = formatter.Format(new FileFormatRequest(path, false, false, NullFormatterLog.Instance));
                    Assert.Equal(!active, result.Status == FileFormatStatus.Skipped);
                    Assert.Empty(result.Diagnostics);
                    Assert.Equal(input, File.ReadAllText(path));
                }
            }
        }
    }
}
