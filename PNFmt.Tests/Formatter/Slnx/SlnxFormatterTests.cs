// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Xunit;

namespace PNFmt.Tests.Formatter.Slnx
{
    public sealed class SlnxFormatterTests
    {
        [Fact]
        public void Dry_run_detects_changes_and_a_second_run_is_unchanged()
        {
            using (var file = TemporaryFile.Create(
                "<Solution><Project Path=\"Z.csproj\" /><Project Path=\"A.csproj\" /></Solution>"))
            {
                var formatter = new SlnxFormatter();
                var log = NullFormatterLog.Instance;
                var original = File.ReadAllText(file.Path);

                var dryRun = formatter.Format(new FileFormatRequest(file.Path, false, false, log));

                Assert.Equal(FileFormatStatus.Updated, dryRun.Status);
                Assert.Equal(original, File.ReadAllText(file.Path));

                var update = formatter.Format(new FileFormatRequest(file.Path, true, false, log));
                var secondRun = formatter.Format(new FileFormatRequest(file.Path, true, false, log));

                Assert.Equal(FileFormatStatus.Updated, update.Status);
                Assert.Equal(FileFormatStatus.Unchanged, secondRun.Status);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("false")]
        [InlineData("invalid")]
        public void Explicit_true_setting_is_required(string settingValue)
        {
            using (var file = TemporaryFile.Create(
                "<Solution><Project Path=\"Z.csproj\" />"
                + "<Project Path=\"A.csproj\" /></Solution>",
                settingValue))
            {
                var formatter = new SlnxFormatter();
                var original = File.ReadAllText(file.Path);
                var result = formatter.Format(
                    new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance));

                Assert.Equal(FileFormatStatus.Skipped, result.Status);
                Assert.Equal(original, File.ReadAllText(file.Path));
            }
        }

        [Theory]
        [InlineData("<Extension>", "</Extension>")]
        [InlineData("<e:Project xmlns:e='urn:extension'>", "</e:Project>")]
        public void Extension_subtrees_preserve_all_character_data(string start, string end)
        {
            var input = "<?xml version='1.0'?><Solution><Project Path='Z.csproj'/>" + start
                + "<Value> </Value><Text xml:space='preserve'>first&#xD;second&#xD;&#xA;third</Text>"
                + "<Compact><A/><B/></Compact><Mixed>left <A/> right</Mixed>"
                + "<!-- extension comment --><![CDATA[keep\nthis]]>" + end
                + "<Project Path='B.csproj'/><Project Path='A.csproj'/></Solution>";
            using (var file = TemporaryFile.Create(input))
            {
                var formatter = new SlnxFormatter();
                var original = XDocument.Parse(input, LoadOptions.PreserveWhitespace).Root.Elements().ElementAt(1);
                Assert.Equal(FileFormatStatus.Updated,
                    formatter.Format(new FileFormatRequest(file.Path, false, false, NullFormatterLog.Instance)).Status);
                Assert.Equal(input, File.ReadAllText(file.Path));
                formatter.Format(new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance));
                var formatted = File.ReadAllText(file.Path);
                var extension = XDocument.Parse(formatted, LoadOptions.PreserveWhitespace).Root.Elements().ElementAt(1);
                Assert.True(XNode.DeepEquals(original, extension), formatted);
                Assert.Equal(FileFormatStatus.Unchanged,
                    formatter.Format(new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance)).Status);
            }
        }

        [Fact]
        public void Invalid_solution_root_is_rejected_without_writing()
        {
            using (var file = TemporaryFile.Create("<Project />"))
            {
                var formatter = new SlnxFormatter();
                var request = new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance);

                Assert.Throws<InvalidDataException>(() => formatter.Format(request));
                Assert.Equal("<Project />", File.ReadAllText(file.Path));
            }
        }

        [Theory]
        [InlineData("\n")]
        [InlineData("\r\n")]
        [InlineData("\r")]
        public void Layout_uses_literal_newlines_while_extension_carriage_returns_stay_escaped(string newline)
        {
            var input = string.Join(newline, "<?xml version='1.0'?>", "<Solution>", "<Folder Name='/Source/'>",
                "<Project Path='Z' />", "<Project Path='A' />", "</Folder>",
                "<Extension>first&#xD;second</Extension>", "</Solution>") + newline;
            var expected = string.Join(newline, "<?xml version=\"1.0\" encoding=\"utf-8\"?>", "<Solution>", "  <Folder Name=\"/Source/\">",
                "    <Project Path=\"A\" />", "    <Project Path=\"Z\" />", "  </Folder>",
                "  <Extension>first&#xD;second</Extension>", "</Solution>") + newline;

            var actual = SlnxDocumentFormatter.Format(input);

            Assert.Equal(expected, actual);
            Assert.Equal("first\rsecond", XDocument.Parse(actual).Root.Element("Extension").Value);
            Assert.Equal(actual, SlnxDocumentFormatter.Format(actual));
        }

        [Theory]
        [InlineData("", "", "Project", "Path")]
        [InlineData("<Folder Name='/'>", "</Folder>", "Project", "Path")]
        [InlineData("<Configurations>", "</Configurations>", "Platform", "Name")]
        [InlineData("<Configurations><ProjectType Extension='.csproj'>", "</ProjectType></Configurations>", "BuildType", "Solution")]
        [InlineData("<Project Path='App.csproj'>", "</Project>", "BuildType", "Solution")]
        [InlineData("<Properties Name='Settings'>", "</Properties>", "Property", "Name")]
        public void Namespaced_elements_with_known_local_names_remain_sort_barriers(string prefix, string suffix, string elementName, string attribute)
        {
            var input = "<Solution xmlns:e='urn:extension'>" + prefix
                + "<" + elementName + " " + attribute + "='Z' />"
                + "<e:" + elementName + " " + attribute + "='M'><Project Path='Z' /><Project Path='A' /></e:" + elementName + ">"
                + "<" + elementName + " " + attribute + "='B' />"
                + "<" + elementName + " " + attribute + "='A' />"
                + suffix + "</Solution>";
            var extensionName = XName.Get(elementName, "urn:extension");
            var originalExtension = XDocument.Parse(input).Descendants(extensionName).Single();

            var formatted = SlnxDocumentFormatter.Format(input);
            var extension = XDocument.Parse(formatted).Descendants(extensionName).Single();

            Assert.Equal(new[] { "Z", "M", "A", "B" },
                extension.Parent.Elements().Select(element => (string)element.Attribute(attribute)));
            Assert.True(XNode.DeepEquals(originalExtension, extension));
            Assert.Equal(formatted, SlnxDocumentFormatter.Format(formatted));
        }

        [Fact]
        public void Preserved_known_containers_keep_their_complete_subtree()
        {
            const string Input = "<Solution xml:space='preserve'> <Project Path='Z'/> <Project Path='A'/> </Solution>";
            var formatted = SlnxDocumentFormatter.Format(Input);
            Assert.True(XNode.DeepEquals(XDocument.Parse(Input, LoadOptions.PreserveWhitespace).Root,
                XDocument.Parse(formatted, LoadOptions.PreserveWhitespace).Root));
        }

        [Fact]
        public void Sorts_solution_folders_projects_configurations_and_properties()
        {
            const string Input =
                "<Solution>"
                + "<Project Path=\"src/Z.csproj\" />"
                + "<!-- Tests stay documented -->"
                + "<Folder Name=\"/Tests/\"><Project Path=\"tests/Z.csproj\" />"
                + "<Project Path=\"tests/A.csproj\" /></Folder>"
                + "<Configurations><Platform Name=\"x64\" /><BuildType Name=\"Release\" />"
                + "<Platform Name=\"Arm64\" /><BuildType Name=\"Debug\" /></Configurations>"
                + "<Folder Name=\"/Source/\"><Project Path=\"src/B.csproj\" />"
                + "<File Path=\"README.md\" /><Project Path=\"src/A.csproj\" />"
                + "<File Path=\"LICENSE\" /></Folder>"
                + "<Project Path=\"src/A.csproj\" />"
                + "<Properties Name=\"RunConfigurations\" Scope=\"PostLoad\">"
                + "<Property Name=\"Zeta\" Value=\"2\" /><Property Name=\"Alpha\" Value=\"1\" />"
                + "</Properties></Solution>";

            var formatted = SlnxDocumentFormatter.Format(Input);
            var document = XDocument.Parse(formatted);
            var root = document.Root;

            Assert.Equal(
                new[] { "Configurations", "Folder", "Folder", "Project", "Project", "Properties" },
                root.Elements().Select(element => element.Name.LocalName));
            Assert.Equal(
                new[] { "/Source/", "/Tests/" },
                root.Elements("Folder").Select(folder => (string)folder.Attribute("Name")));
            Assert.Equal(
                new[] { "BuildType:Debug", "BuildType:Release", "Platform:Arm64", "Platform:x64" },
                root.Element("Configurations").Elements()
                    .Select(element => $"{element.Name.LocalName}:{(string)element.Attribute("Name")}"));

            var source = root.Elements("Folder").Single(
                folder => (string)folder.Attribute("Name") == "/Source/");
            Assert.Equal(
                new[] { "File:LICENSE", "File:README.md", "Project:src/A.csproj", "Project:src/B.csproj" },
                source.Elements().Select(
                    element => $"{element.Name.LocalName}:{(string)element.Attribute("Path")}"));
            Assert.Equal(
                new[] { "Alpha", "Zeta" },
                root.Element("Properties").Elements("Property")
                    .Select(property => (string)property.Attribute("Name")));

            var tests = root.Elements("Folder").Single(
                folder => (string)folder.Attribute("Name") == "/Tests/");
            Assert.IsType<XComment>(tests.PreviousNode);
            Assert.Contains("\n  <Folder", formatted);
        }

        [Fact]
        public void Unknown_extension_elements_are_sort_barriers()
        {
            const string Input =
                "<Solution><Project Path=\"Z.csproj\" /><Extension Value=\"keep\" />"
                + "<Project Path=\"B.csproj\" /><Project Path=\"A.csproj\" /></Solution>";

            var root = XDocument.Parse(SlnxDocumentFormatter.Format(Input)).Root;

            Assert.Equal(
                new[] { "Project:Z.csproj", "Extension:keep", "Project:A.csproj", "Project:B.csproj" },
                root.Elements().Select(element =>
                    $"{element.Name.LocalName}:{(string)(element.Attribute("Path") ?? element.Attribute("Value"))}"));
        }

        private sealed class TemporaryFile : IDisposable
        {
            private readonly TestDirectory directory;

            private TemporaryFile(TestDirectory directory, string path)
            {
                this.directory = directory;
                this.Path = path;
            }

            public string DirectoryPath => this.directory.Path;

            public string Path { get; }

            public static TemporaryFile Create(string contents, string settingValue = "true")
            {
                var directory = new TestDirectory();
                if (settingValue is not null)
                {
                    directory.Write(
                        ".editorconfig",
                        "root = true\n\n[*.slnx]\npnfmt_sort_entries = " + settingValue + "\n");
                }

                var path = directory.Write("Solution.slnx", contents);
                return new TemporaryFile(directory, path);
            }

            public void Dispose()
            {
                this.directory.Dispose();
            }
        }

    }
}
