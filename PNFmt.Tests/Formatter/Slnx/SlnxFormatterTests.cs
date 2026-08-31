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

        [Fact]
        public void Dry_run_detects_changes_and_a_second_run_is_unchanged()
        {
            using (var file = TemporaryFile.Create(
                "<Solution><Project Path=\"Z.csproj\" /><Project Path=\"A.csproj\" /></Solution>"))
            {
                var formatter = new SlnxFormatter();
                var log = new TestLog();
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

        [Fact]
        public void Invalid_solution_root_is_rejected_without_writing()
        {
            using (var file = TemporaryFile.Create("<Project />"))
            {
                var formatter = new SlnxFormatter();
                var request = new FileFormatRequest(file.Path, true, false, new TestLog());

                Assert.Throws<InvalidDataException>(() => formatter.Format(request));
                Assert.Equal("<Project />", File.ReadAllText(file.Path));
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
                    new FileFormatRequest(file.Path, true, false, new TestLog()));

                Assert.Equal(FileFormatStatus.Skipped, result.Status);
                Assert.Equal(original, File.ReadAllText(file.Path));
            }
        }

        private sealed class TemporaryFile : IDisposable
        {
            private TemporaryFile(string directoryPath, string path)
            {
                this.DirectoryPath = directoryPath;
                this.Path = path;
            }

            public string DirectoryPath { get; }

            public string Path { get; }

            public static TemporaryFile Create(string contents, string settingValue = "true")
            {
                var directory = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "PNFmtSlnxTests",
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                if (settingValue is not null)
                {
                    File.WriteAllText(
                        System.IO.Path.Combine(directory, ".editorconfig"),
                        "root = true\n\n[*.slnx]\npnfmt_sort_entries = " + settingValue + "\n");
                }

                var path = System.IO.Path.Combine(directory, "Solution.slnx");
                File.WriteAllText(path, contents);
                return new TemporaryFile(directory, path);
            }

            public void Dispose()
            {
                Directory.Delete(this.DirectoryPath, true);
            }
        }

        private sealed class TestLog : IFormatterLog
        {
            public void Write(Exception exception)
            {
            }

            public void WriteLine(string message)
            {
            }
        }
    }
}
