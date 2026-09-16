// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PNFmt.Cli;
using Xunit;

namespace PNFmt.Tests.Formatter.EditorConfig
{
    public sealed class FormatterOptionMatrixTests
    {
        // Deliberately unsorted and unformatted inputs make both enabled and
        // disabled operations observable, rather than merely checking for errors.
        private static readonly IReadOnlyDictionary<string, Sample> Samples = new Dictionary<string, Sample>
        {
            ["csharp"] = new Sample("Sample.cs", "using Z;\nusing A;\nclass C{\nvoid M(){}\n}", "using Z;", "using A;", "class C\n{"),
            ["csproj"] = new Sample("Sample.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><Z>2</Z><A>1</A></PropertyGroup></Project>", "<Z>", "<A>", "\n  <PropertyGroup>"),
            ["ini"] = new Sample("Sample.ini", "z=2\na=1", "z", "a", " = "),
            ["resx"] = new Sample("Sample.resx", "<root><resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader><data name=\"z\"><value>2</value></data><data name=\"a\"><value>1</value></data></root>", "name=\"z\"", "name=\"a\"", "\n  <data"),
            ["rsp"] = new Sample("Sample.rsp", "z.cs  \na.cs  ", "z.cs", "a.cs", "z.cs\n"),
            ["slnx"] = new Sample("Sample.slnx", "<Solution><Project Path=\"z.csproj\" /><Project Path=\"a.csproj\" /></Solution>", "z.csproj", "a.csproj", "\n  <Project"),
            ["xml"] = new Sample("Sample.xml", "<root><z/><a/></root>", "<z/>", "<a/>", "\n  <z/>", supportsSorting: false),
            ["xaml"] = new Sample("Sample.xaml", "<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><Button Name=\"Z\"/><Button Name=\"A\"/></Grid>", "Name=\"Z\"", "Name=\"A\"", "\n  <Button", supportsSorting: false),
        };

        public static IEnumerable<object[]> CommonCombinations => Samples.Keys.SelectMany(
            name => Combinations(3).Select(options => new object[] { name }.Concat(options).ToArray()));

        public static IEnumerable<object[]> CSharpCombinations => Combinations(8);

        public static IEnumerable<object[]> IniCombinations => Combinations(6);

        public static IEnumerable<object[]> ResxCombinations => Combinations(9);

        [Theory]
        [MemberData(nameof(CommonCombinations))]
        public void Enablement_layout_and_sorting_follow_the_full_matrix(
            string name, bool enabled, bool format, bool sort)
        {
            var sample = Samples[name];
            var output = Exercise(name, sample.FileName, sample.Input,
                Settings(("pnfmt_enabled", enabled), ("pnfmt_format", format), ("pnfmt_sort_entries", sort)),
                enabled, enabled && (format || (sort && sample.SupportsSorting)));

            var first = enabled && sort && sample.SupportsSorting ? sample.Second : sample.First;
            var second = enabled && sort && sample.SupportsSorting ? sample.First : sample.Second;
            AssertOrder(output, first, second);
            Assert.Equal(enabled && format, output.Contains(sample.LayoutMarker, StringComparison.Ordinal));
        }

        [Theory]
        [MemberData(nameof(CSharpCombinations))]
        public void Csharp_switches_follow_every_boolean_combination(
            bool enabled, bool format, bool sortImports, bool sortModifiers,
            bool sortMembers, bool sortMembersByName, bool collapseBlankLines, bool removeRegions)
        {
            const string Input = "using Z;\nusing A;\n#region Wrapper\nclass C{\n"
                + "static public void Z(){}\npublic int P=>1;\nstatic public void A(){}\n}\n#endregion\n"
                + "class D{}\n\n\n\nclass E{}\n";
            var output = Exercise("csharp", "Sample.cs", Input,
                Settings(("pnfmt_enabled", enabled), ("pnfmt_format", format), ("pnfmt_sort_entries", sortImports),
                    ("pnfmt_csharp_sort_modifiers", sortModifiers), ("pnfmt_csharp_sort_members", sortMembers),
                    ("pnfmt_csharp_sort_members_by_name", sortMembersByName), ("pnfmt_csharp_collapse_blank_lines", collapseBlankLines),
                    ("pnfmt_csharp_remove_regions", removeRegions)),
                enabled, enabled && (format || sortImports || sortModifiers || sortMembers || collapseBlankLines || removeRegions));

            var root = CSharpSyntaxTree.ParseText(output).GetCompilationUnitRoot();
            Assert.Equal(enabled && sortImports ? new[] { "A", "Z" } : new[] { "Z", "A" },
                root.Usings.Select(item => item.Name.ToString()));
            var type = root.Members.OfType<ClassDeclarationSyntax>().Single(item => item.Identifier.ValueText == "C");
            var expectedMembers = enabled && sortMembers
                ? sortMembersByName ? new[] { "P", "A", "Z" } : new[] { "P", "Z", "A" }
                : new[] { "Z", "P", "A" };
            Assert.Equal(expectedMembers, type.Members.Select(member => member is MethodDeclarationSyntax method
                ? method.Identifier.ValueText : ((PropertyDeclarationSyntax)member).Identifier.ValueText));
            foreach (var method in type.Members.OfType<MethodDeclarationSyntax>())
            {
                Assert.Equal(enabled && sortModifiers ? new[] { "public", "static" } : new[] { "static", "public" },
                    method.Modifiers.Select(token => token.Text));
                Assert.Empty(method.Body.Statements);
            }

            Assert.Equal(!(enabled && removeRegions), output.Contains("#region Wrapper", StringComparison.Ordinal));
            Assert.Equal(!(enabled && removeRegions), output.Contains("#endregion", StringComparison.Ordinal));
            var types = root.Members.OfType<ClassDeclarationSyntax>().ToArray();
            var gap = output.Substring(types[1].Span.End, types[2].SpanStart - types[1].Span.End);
            Assert.Equal(enabled && collapseBlankLines ? 2 : 4, Regex.Matches(gap, "\r\n|\r|\n").Count);
            Assert.Equal(enabled && format, output.Contains("class C\n{", StringComparison.Ordinal));
            Assert.Equal("1", type.Members.OfType<PropertyDeclarationSyntax>().Single().ExpressionBody.Expression.ToString());
        }

        [Theory]
        [MemberData(nameof(IniCombinations))]
        public void Ini_switches_follow_every_boolean_combination(
            bool enabled, bool format, bool sortEntries, bool groupByPrefix, bool sortGroups, bool mergeGroups)
        {
            const string Input = "[z]\nb_z=1\na_z=2\nb_a=3\na_a=4\n\n[a]\nz=6\na=7\n\n[z]\nc=5\n";
            var output = Exercise("ini", "Sample.ini", Input,
                Settings(("pnfmt_enabled", enabled), ("pnfmt_format", format), ("pnfmt_sort_entries", sortEntries),
                    ("pnfmt_ini_group_by_prefix", groupByPrefix), ("pnfmt_ini_sort_groups", sortGroups), ("pnfmt_ini_merge_groups", mergeGroups)),
                enabled, enabled && (format || sortEntries || groupByPrefix || sortGroups || mergeGroups));

            var headers = Regex.Matches(output, @"(?m)^\[([^\]]+)\]").Cast<Match>().ToArray();
            var expectedHeaders = enabled && sortGroups
                ? enabled && mergeGroups ? new[] { "a", "z" } : new[] { "a", "z", "z" }
                : enabled && mergeGroups ? new[] { "z", "a" } : new[] { "z", "a", "z" };
            Assert.Equal(expectedHeaders, headers.Select(match => match.Groups[1].Value));
            var groupIndex = Array.FindIndex(headers, match => match.Groups[1].Value == "z");
            var start = headers[groupIndex].Index + headers[groupIndex].Length;
            var end = groupIndex + 1 < headers.Length ? headers[groupIndex + 1].Index : output.Length;
            var body = output.Substring(start, end - start).Trim();
            var expectedKeys = enabled && sortEntries ? new[] { "a_a", "a_z", "b_a", "b_z" }
                : enabled && groupByPrefix ? new[] { "b_z", "b_a", "a_z", "a_a" }
                : new[] { "b_z", "a_z", "b_a", "a_a" };
            Assert.Equal(enabled && mergeGroups ? expectedKeys.Concat(new[] { "c" }) : expectedKeys,
                Assignments(body).Select(pair => pair.Key));
            Assert.Equal(enabled && groupByPrefix, body.Contains("\n\n", StringComparison.Ordinal));
            Assert.Contains(enabled && format ? "b_z = 1" : "b_z=1", output);
            Assert.Equal(Assignments(Input).OrderBy(pair => pair.Key), Assignments(output).OrderBy(pair => pair.Key));
        }

        [Theory]
        [MemberData(nameof(ResxCombinations))]
        public void Resx_switches_follow_every_boolean_combination_with_content_present_or_absent(
            bool enabled, bool format, bool sortEntries, bool insertComment, bool removeComment,
            bool insertSchema, bool removeSchema, bool hasComment, bool hasSchema)
        {
            var input = "<root>"
                + (hasComment ? ResxSchemaDefaults.OriginalComment.Trim() : "")
                + (hasSchema ? "<xs:schema xmlns:xs=\"http://www.w3.org/2001/XMLSchema\" id=\"root\"/>" : "")
                + "<resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>"
                + "<!--user comment--><data name=\"z\"><value>two  spaces</value></data>"
                + "<data name=\"a\"><value>one</value></data></root>";
            var changed = enabled && (format || sortEntries
                || (hasComment ? removeComment : insertComment && !removeComment)
                || (hasSchema ? removeSchema : insertSchema && !removeSchema));
            var output = Exercise("resx", "Sample.resx", input,
                Settings(("pnfmt_enabled", enabled), ("pnfmt_format", format), ("pnfmt_sort_entries", sortEntries),
                    ("pnfmt_resx_insert_documentation_comment", insertComment), ("pnfmt_resx_remove_documentation_comment", removeComment),
                    ("pnfmt_resx_insert_xsd_schema", insertSchema), ("pnfmt_resx_remove_xsd_schema", removeSchema)),
                enabled, changed);

            var root = XDocument.Parse(output, LoadOptions.PreserveWhitespace).Root;
            Assert.Equal(enabled ? !removeComment && (hasComment || insertComment) : hasComment,
                root.Nodes().OfType<XComment>().Any(comment => comment.Value.Contains("Microsoft ResX Schema", StringComparison.Ordinal)));
            Assert.Equal(enabled ? !removeSchema && (hasSchema || insertSchema) : hasSchema,
                root.Elements(XName.Get("schema", "http://www.w3.org/2001/XMLSchema")).Any());
            Assert.Equal(enabled && sortEntries ? new[] { "a", "z" } : new[] { "z", "a" },
                root.Elements("data").Select(element => (string)element.Attribute("name")));
            var entry = root.Elements("data").Single(element => (string)element.Attribute("name") == "z");
            Assert.Equal("two  spaces", (string)entry.Element("value"));
            Assert.Equal("one", (string)root.Elements("data").Single(element => (string)element.Attribute("name") == "a").Element("value"));
            Assert.Contains(root.Nodes().OfType<XComment>(), comment => comment.Value == "user comment");
            var preceding = root.Element("resheader").PreviousNode as XText;
            Assert.Equal(enabled && format, preceding?.Value.Contains("\n  ", StringComparison.Ordinal) == true);
        }

        private static IEnumerable<object[]> Combinations(int switchCount)
        {
            for (var mask = 0; mask < (1 << switchCount); mask++)
            {
                yield return Enumerable.Range(0, switchCount).Select(bit => (object)((mask & (1 << bit)) != 0)).ToArray();
            }
        }

        private static string Settings(params (string Name, bool Value)[] settings)
        {
            return string.Concat(settings.Select(setting => setting.Name + " = " + (setting.Value ? "true" : "false") + "\n"));
        }

        private static string Exercise(string name, string fileName, string input, string options, bool enabled, bool changed)
        {
            using var directory = new TestDirectory();
            directory.Write(".editorconfig", "root = true\n[*]\npnfmt_formatter = " + name + "\n"
                + "indent_size = 2\nend_of_line = lf\ninsert_final_newline = true\n" + options);
            var path = directory.Write(fileName, input);
            var original = File.ReadAllBytes(path);
            var runner = new FormattingRunner(FormatterCatalog.CreateDefault());
            var status = !enabled ? FileFormatStatus.Skipped : changed ? FileFormatStatus.Updated : FileFormatStatus.Unchanged;

            AssertOutcome(runner.Run(new[] { path }, false, false, 1).Outcomes[0], status);
            Assert.Equal(original, File.ReadAllBytes(path));
            AssertOutcome(runner.Run(new[] { path }, true, false, 1).Outcomes[0], status);
            var formatted = File.ReadAllBytes(path);
            Assert.Equal(changed, !original.SequenceEqual(formatted));
            AssertOutcome(runner.Run(new[] { path }, true, false, 1).Outcomes[0],
                enabled ? FileFormatStatus.Unchanged : FileFormatStatus.Skipped);
            Assert.Equal(formatted, File.ReadAllBytes(path));
            return File.ReadAllText(path);
        }

        private static void AssertOutcome(FileFormattingOutcome outcome, FileFormatStatus status)
        {
            Assert.Null(outcome.Error);
            Assert.Empty(outcome.LoggedExceptions);
            Assert.Empty(outcome.Result.Diagnostics);
            Assert.DoesNotContain(outcome.LogMessages, message => message.Kind == FormatterLogMessageKind.Warning);
            Assert.Equal(status, outcome.Result.Status);
        }

        private static void AssertOrder(string text, string first, string second)
        {
            var firstIndex = text.IndexOf(first, StringComparison.Ordinal);
            var secondIndex = text.IndexOf(second, StringComparison.Ordinal);
            Assert.True(firstIndex >= 0 && secondIndex > firstIndex, $"Expected '{first}' before '{second}' in:\n{text}");
        }

        private static IEnumerable<KeyValuePair<string, string>> Assignments(string text)
        {
            return Regex.Matches(text, @"(?m)^([^\[\]\r\n=]+?)\s*=\s*([^\r\n]*)").Cast<Match>()
                .Select(match => new KeyValuePair<string, string>(match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim()));
        }

        private sealed class Sample
        {
            public Sample(string fileName, string input, string first, string second, string layoutMarker, bool supportsSorting = true)
            {
                this.FileName = fileName;
                this.Input = input;
                this.First = first;
                this.Second = second;
                this.LayoutMarker = layoutMarker;
                this.SupportsSorting = supportsSorting;
            }

            public string FileName { get; }
            public string Input { get; }
            public string First { get; }
            public string Second { get; }
            public string LayoutMarker { get; }
            public bool SupportsSorting { get; }
        }
    }
}
