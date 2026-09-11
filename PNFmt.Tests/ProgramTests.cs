// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using LibGit2Sharp;
using PNFmt.Cli;
using Xunit;

namespace PNFmt.Tests
{
    public sealed class ProgramTests
    {
        [Theory]
        [InlineData("--check")]
        [InlineData("--dry-run")]
        [InlineData("--lint")]
        [InlineData("--all")]
        public void Unreadable_editorconfig_is_an_error_without_writing(string mode)
        {
            using (var directory = new TemporaryDirectory())
            {
                var configuration = directory.Write(".editorconfig", "root = true\n[*.xml]\npnfmt_xml_format = true\n");
                var path = directory.Write("Data.xml", "<root><child /></root>");
                var original = File.ReadAllBytes(path);
                using (var fileLock = new FileStream(configuration, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var result = Run("--all", mode, path);
                    Assert.Equal(2, result.ExitCode);
                    Assert.Contains("EditorConfig", result.Error);
                    Assert.Contains("failed 1", result.Output);
                    Assert.Equal(original, File.ReadAllBytes(path));
                }
            }
        }

        [Fact]
        public void Check_and_dry_run_detect_resx_bom_changes_without_writing()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.Write(".editorconfig", "root = true\n[*.resx]\ncharset = utf-8\n"
                    + "pnfmt_sort_entries = true\npnfmt_resx_remove_xsd_schema = true\n"
                    + "pnfmt_resx_remove_documentation_comment = true\n");
                var path = directory.Write("Resources.resx", "<root>"
                    + "<resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>"
                    + "<data name=\"Text\"><value>hello</value></data></root>");
                Assert.Equal(0, Run("--all", path).ExitCode);
                var formatted = File.ReadAllBytes(path);
                File.WriteAllBytes(path, new UTF8Encoding(true).GetPreamble().Concat(formatted).ToArray());
                var withBom = File.ReadAllBytes(path);

                Assert.Equal(1, Run("--all", "--check", path).ExitCode);
                Assert.Equal(0, Run("--all", "--dry-run", path).ExitCode);
                Assert.Equal(withBom, File.ReadAllBytes(path));
                Assert.Equal(0, Run("--all", path).ExitCode);
                Assert.Equal(formatted, File.ReadAllBytes(path));
                Assert.Equal(0, Run("--all", "--check", path).ExitCode);
            }
        }

        [Fact]
        public void Xml_and_xaml_cli_support_defaults_preview_filtering_and_nested_indentation()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                directory.Write("nested/.editorconfig", "[*.xaml]\nindent_size = 2\n");
                var xml = directory.Write("Data.xml", "<root><z/><a/></root>");
                var xaml = directory.Write("nested/View.xaml",
                    "<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock><Run Text='A'/><Run Text='B'/></TextBlock><Button/></Grid>");
                var unrelated = directory.Write("Other.ini", "z=2\na=1");
                var originalXml = File.ReadAllText(xml);
                var originalXaml = File.ReadAllText(xaml);
                Assert.Equal(1, Run("--all", "--check", "--recursive", "--formatter", "xml,xaml", directory.Path).ExitCode);
                Assert.Equal(0, Run("--all", "--dry-run", "--recursive", "--formatter", "xml,xaml", directory.Path).ExitCode);
                Assert.Equal(originalXml, File.ReadAllText(xml));
                Assert.Equal(originalXaml, File.ReadAllText(xaml));
                var result = Run("--all", "--recursive", "--formatter", "xml,xaml", "-m:4", directory.Path);
                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Updated 2", result.Output);
                Assert.Equal("<root>\n    <z/>\n    <a/>\n</root>", File.ReadAllText(xml));
                Assert.Contains("\n  <TextBlock><Run Text='A'/><Run Text='B'/></TextBlock>\n  <Button/>\n", File.ReadAllText(xaml));
                Assert.Equal("z=2\na=1", File.ReadAllText(unrelated));
                Assert.Equal(0, Run("--all", "--check", "--recursive", "--formatter", "xml,xaml", directory.Path).ExitCode);
            }
        }

        [Theory]
        [InlineData("Broken.xml", "XML001")]
        [InlineData("Broken.xaml", "XAML001")]
        public void Xml_and_xaml_cli_report_invalid_input_without_writing(string name, string code)
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                var file = directory.Write(name, "<!DOCTYPE root SYSTEM 'file:///missing.dtd'><root/>");
                var original = File.ReadAllBytes(file);
                var result = Run("--lint", file);
                Assert.Equal(1, result.ExitCode);
                Assert.Contains("warning " + code, result.Output);
                Assert.Contains("skipped 1", result.Output);
                Assert.Equal(original, File.ReadAllBytes(file));
            }
        }

        [Fact]
        public void Csharp_cli_preserves_encoding_skips_generated_files_and_honors_exclusions()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                const string Protected = "// pnfmt: off\nstatic public void Keep( ){ }\n// pnfmt: on\n";
                var input = "static public class C{\nstatic private int A;\n\n\nstatic private int B;\n"
                    + Protected + "}\n";
                var source = directory.Write("Source.cs", input);
                var encoding = new System.Text.UnicodeEncoding(false, true, true);
                File.WriteAllText(source, input, encoding);
                var generated = directory.Write("Source.g.cs", "// Generated\nclass Invalid{");
                var original = File.ReadAllBytes(source);

                var preview = Run("--all", "--check", "--formatter", "csharp", directory.Path);
                Assert.Equal(1, preview.ExitCode);
                Assert.Equal(original, File.ReadAllBytes(source));
                var result = Run("--all", "--formatter", "csharp", directory.Path);
                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Updated 1, unchanged 0, skipped 1", result.Output);
                var bytes = File.ReadAllBytes(source);
                Assert.Equal(encoding.GetPreamble(), bytes.Take(2));
                var text = encoding.GetString(bytes, 2, bytes.Length - 2);
                Assert.Contains("public static class C", text);
                Assert.Contains("private static int A;\n\n    private static int B;", text);
                Assert.Contains(Protected, text);
                Assert.Equal("// Generated\nclass Invalid{", File.ReadAllText(generated));
                Assert.Equal(0, Run("--all", "--check", "--formatter", "csharp", directory.Path).ExitCode);
            }
        }

        [Fact]
        public void Csharp_cli_formats_a_folder_without_projects_and_honors_nested_settings()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                directory.Write("nested/.editorconfig", "[*.cs]\nindent_size = 2\n");
                var first = directory.Write("First.cs", "using Z;\nusing A;\nclass C{\nvoid M(){ }\n}\n");
                var nested = directory.Write("nested/Second.cs", "class D{\nvoid M(){ }\n}\n");
                var project = directory.Write("Untouched.csproj", "<Project />");
                var original = File.ReadAllText(first);

                var check = Run("--all", "--check", "--recursive", "--formatter", "csharp", directory.Path);
                Assert.Equal(1, check.ExitCode);
                Assert.Equal(original, File.ReadAllText(first));
                var preview = Run("--all", "--dry-run", "--recursive", "--formatter", "csharp", directory.Path);
                Assert.Equal(0, preview.ExitCode);
                Assert.Equal(original, File.ReadAllText(first));

                // No project is needed, even when an unrelated project file is invalid.
                File.WriteAllText(project, "not an MSBuild project");
                var format = Run("--all", "--recursive", "--formatter", "csharp", "-m:4", directory.Path);
                Assert.Equal(0, format.ExitCode);
                Assert.Contains("Updated 2", format.Output);
                Assert.StartsWith("using A;\nusing Z;", File.ReadAllText(first));
                Assert.Contains("\n    void M()", File.ReadAllText(first));
                Assert.Contains("\n  void M()", File.ReadAllText(nested));
                Assert.Equal("not an MSBuild project", File.ReadAllText(project));
                Assert.Equal(0, Run("--all", "--check", "--recursive", "--formatter", "csharp", directory.Path).ExitCode);
            }
        }

        [Fact]
        public void Csharp_cli_reports_skipped_syntax_errors_and_lint_fails_without_writing()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                var source = directory.Write("Invalid.cs", "class C{");
                var format = Run(source);
                var lint = Run("--lint", source);

                Assert.Equal(0, format.ExitCode);
                Assert.Contains("warning PNFMT002", format.Output);
                Assert.Equal(1, lint.ExitCode);
                Assert.Contains("skipped 1", lint.Output);
                Assert.Equal("class C{", File.ReadAllText(source));
            }
        }

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
                Assert.Contains("Processed 2 file(s) in ", checkResult.Output);
                Assert.Contains("Would update 2", checkResult.Output);
                Assert.DoesNotContain("[would-update]", checkResult.Output);
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
                var project = directory.Write(
                    Path.Combine("nested", "Project.csproj"),
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"
                    + "<Zeta>z</Zeta><Alpha>a</Alpha></PropertyGroup></Project>");
                var resource = directory.Write(
                    Path.Combine("nested", "Strings.resx"),
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?><root>"
                    + "<resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>"
                    + "<data name=\"b\"><value>b</value></data>"
                    + "<data name=\"a\"><value>a</value></data></root>");
                var solution = directory.Write(
                    Path.Combine("nested", "Solution.slnx"),
                    "<Solution><Project Path=\"Z.csproj\" />"
                    + "<Project Path=\"A.csproj\" /></Solution>");
                var ini = directory.Write(
                    Path.Combine("nested", "settings.ini"),
                    "z=2\r\na=1\r\n");
                var response = directory.Write(
                    Path.Combine("nested", "compiler.rsp"),
                    "zeta.cs\r\nalpha.cs\r\n");

                var result = Run("--recursive", directory.Path);

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Processed 6", result.Output);
                Assert.Contains("Updated 5", result.Output);
                Assert.True(
                    File.ReadAllText(project).IndexOf("<Alpha>", StringComparison.Ordinal)
                    < File.ReadAllText(project).IndexOf("<Zeta>", StringComparison.Ordinal));
                Assert.Equal(new[] { "a", "b" }, TemporaryDirectory.ReadNames(resource));
                Assert.True(
                    File.ReadAllText(solution).IndexOf("A.csproj", StringComparison.Ordinal)
                    < File.ReadAllText(solution).IndexOf("Z.csproj", StringComparison.Ordinal));
                Assert.Equal("a = 1\r\nz = 2\r\n", File.ReadAllText(ini));
                Assert.Equal("alpha.cs\r\nzeta.cs\r\n", File.ReadAllText(response));
            }
        }

        [Fact]
        public void Editorconfig_files_are_discovered_by_exact_file_name()
        {
            using (var directory = new TemporaryDirectory())
            {
                var editorConfig = directory.Write(
                    ".editorconfig",
                    "root=true\n"
                    + "z=2\n"
                    + "a=1\n"
                    + "\n"
                    + "[*.editorconfig]\n"
                    + "pnfmt_sort_entries=true\n");

                var result = Run(directory.Path);

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Updated 1", result.Output);
                Assert.Equal(
                    "a = 1\nroot = true\nz = 2\n"
                    + "\n"
                    + "[*.editorconfig]\n"
                    + "pnfmt_sort_entries = true\n",
                    File.ReadAllText(editorConfig));
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

        [Fact]
        public void Help_and_version_are_available()
        {
            var help = Run("--help");
            var version = Run("--version");

            Assert.Equal(0, help.ExitCode);
            Assert.Contains("Usage: pnfmt", help.Output);
            Assert.Contains("--all", help.Output);
            Assert.Contains("-m[:N], -maxCpuCount[:N]", help.Output);
            Assert.Contains("--file-pattern", help.Output);
            Assert.Contains("--formatter", help.Output);
            Assert.Contains("--write-default-config", help.Output);
            Assert.Contains("--migrate-legacy-config", help.Output);
            Assert.Contains("--remove-legacy-config", help.Output);
            Assert.Contains(".rsp", help.Output);
            Assert.Equal(0, version.ExitCode);
            Assert.StartsWith("pnfmt ", version.Output);
        }

        [Fact]
        public void Git_status_filters_the_existing_directory_scope_unless_all_is_requested()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                var changed = directory.Write("Changed.ini", "z=2\na=1\n");
                var clean = directory.Write("Clean.ini", "z=2\na=1\n");
                var nested = directory.Write(
                    Path.Combine("nested", "Nested.ini"),
                    "z=2\na=1\n");
                string staged;
                string untracked;
                Repository.Init(directory.Path);
                using (var repository = new Repository(directory.Path))
                {
                    Commands.Stage(repository, ".editorconfig");
                    Commands.Stage(repository, "Changed.ini");
                    Commands.Stage(repository, "Clean.ini");
                    Commands.Stage(repository, "nested/Nested.ini");
                    var signature = new Signature(
                        "PNFmt Tests",
                        "tests@example.invalid",
                        DateTimeOffset.UtcNow);
                    repository.Commit("Initial", signature, signature);
                    staged = directory.Write("Staged.ini", "z=2\na=1\n");
                    untracked = directory.Write("Untracked.ini", "z=2\na=1\n");
                    Commands.Stage(repository, "Staged.ini");
                }

                File.AppendAllText(changed, "\n");
                File.AppendAllText(nested, "\n");

                var currentDirectoryResult = RunInDirectory(directory.Path);

                Assert.Equal(0, currentDirectoryResult.ExitCode);
                Assert.Contains("Processed 3 file(s)", currentDirectoryResult.Output);
                Assert.Equal("a = 1\nz = 2\n", File.ReadAllText(changed));
                Assert.Equal("a = 1\nz = 2\n", File.ReadAllText(staged));
                Assert.Equal("a = 1\nz = 2\n", File.ReadAllText(untracked));
                Assert.Equal("z=2\na=1\n", File.ReadAllText(clean));
                Assert.Equal("z=2\na=1\n\n", File.ReadAllText(nested));

                var recursiveResult = RunInDirectory(directory.Path, "--recursive");

                Assert.Equal(0, recursiveResult.ExitCode);
                Assert.Equal("a = 1\nz = 2\n", File.ReadAllText(nested));
                Assert.Equal("z=2\na=1\n", File.ReadAllText(clean));

                var allResult = RunInDirectory(directory.Path, "--all");

                Assert.Equal(0, allResult.ExitCode);
                Assert.Equal("a = 1\nz = 2\n", File.ReadAllText(clean));
            }
        }

        [Fact]
        public void Default_configuration_command_writes_the_requested_directory()
        {
            using (var directory = new TemporaryDirectory())
            {
                var first = Run("--write-default-config", directory.Path);
                var editorConfig = Path.Combine(directory.Path, ".editorconfig");
                var pnfmtConfig = Path.Combine(directory.Path, ".pnfmt");
                var expected = DefaultEditorConfigDocument.Update(string.Empty);

                Assert.Equal(0, first.ExitCode);
                Assert.Contains("Wrote default PNFmt configuration", first.Output);
                Assert.Contains("Wrote default PNFmt tool settings", first.Output);
                Assert.Equal(expected, File.ReadAllText(editorConfig));
                Assert.Equal(
                    "{\n  \"maxCpuCount\": 4\n}\n".Replace("\n", Environment.NewLine),
                    File.ReadAllText(pnfmtConfig));

                const string ExistingToolSettings = "{\n  \"maxCpuCount\": 7\n}\n";
                File.WriteAllText(pnfmtConfig, ExistingToolSettings);

                var second = Run("--write-default-config", editorConfig);

                Assert.Equal(0, second.ExitCode);
                Assert.Contains("already current", second.Output);
                Assert.Contains("left unchanged", second.Output);
                Assert.Equal(expected, File.ReadAllText(editorConfig));
                Assert.Equal(ExistingToolSettings, File.ReadAllText(pnfmtConfig));

                var formatCheck = Run("--check", editorConfig);

                Assert.Equal(0, formatCheck.ExitCode);
                Assert.Contains("unchanged 1", formatCheck.Output);
            }
        }

        [Fact]
        public void Default_tool_settings_are_written_at_the_git_repository_root()
        {
            using (var directory = new TemporaryDirectory())
            {
                Repository.Init(directory.Path);
                var nested = Path.Combine(directory.Path, "nested");
                Directory.CreateDirectory(nested);

                var result = Run("--write-default-config", nested);

                Assert.Equal(0, result.ExitCode);
                Assert.True(File.Exists(Path.Combine(nested, ".editorconfig")));
                Assert.True(File.Exists(Path.Combine(directory.Path, ".pnfmt")));
                Assert.False(File.Exists(Path.Combine(nested, ".pnfmt")));
            }
        }

        [Fact]
        public void Default_editorconfig_creation_does_not_overwrite_a_concurrent_file()
        {
            using (var directory = new TemporaryDirectory())
            {
                var editorConfig = Path.Combine(directory.Path, ".editorconfig");
                var writer = DefaultEditorConfigWriter.Open(editorConfig);
                const string ConcurrentContents = "root = false\n";
                File.WriteAllText(editorConfig, ConcurrentContents);

                var exception = Assert.Throws<IOException>(
                    () => writer.Write(
                        migrateLegacySettings: false,
                        removeLegacySettings: false));

                Assert.Contains("created while defaults were being prepared", exception.Message);
                Assert.Equal(ConcurrentContents, File.ReadAllText(editorConfig));
            }
        }

        [Fact]
        public void Default_configuration_command_rejects_formatting_options()
        {
            var result = Run("--write-default-config", "--recursive");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("cannot be combined", result.Error);
        }

        [Fact]
        public void Default_configuration_prompts_to_migrate_and_keep_legacy_settings()
        {
            using (var directory = new TemporaryDirectory())
            {
                var editorConfig = directory.Write(
                    ".editorconfig",
                    "root = true\n\n[*.resx]\n"
                    + "resx_formatter_sort_entries = false\n");

                var result = RunWithInput(
                    "yes\nno\n",
                    "--write-default-config",
                    editorConfig);
                var updated = File.ReadAllText(editorConfig);

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Migrate 1 legacy formatter setting", result.Output);
                Assert.Contains("Remove the 1 legacy formatter setting", result.Output);
                Assert.Contains("resx_formatter_sort_entries = false", updated);
                Assert.Contains("pnfmt_sort_entries = false", updated);
            }
        }

        [Fact]
        public void Default_configuration_migration_answers_can_be_passed_as_options()
        {
            using (var directory = new TemporaryDirectory())
            {
                var editorConfig = directory.Write(
                    ".editorconfig",
                    "root = true\n\n[*.resx]\n"
                    + "resx_formatter_sort_comparer = InvariantCulture\n");

                var result = Run(
                    "--write-default-config",
                    "--migrate-legacy-config=true",
                    "--remove-legacy-config=true",
                    editorConfig);
                var updated = File.ReadAllText(editorConfig);

                Assert.Equal(0, result.ExitCode);
                Assert.DoesNotContain("Migrate", result.Output);
                Assert.DoesNotContain("resx_formatter_sort_comparer", updated);
                Assert.Contains("pnfmt_resx_sort_comparer = InvariantCulture", updated);
            }
        }

        [Fact]
        public void Default_configuration_migration_options_are_validated()
        {
            var withoutCommand = Run("--migrate-legacy-config=true");
            var invalidBoolean = Run(
                "--write-default-config",
                "--remove-legacy-config=perhaps");

            Assert.Equal(2, withoutCommand.ExitCode);
            Assert.Contains("require '--write-default-config'", withoutCommand.Error);
            Assert.Equal(2, invalidBoolean.ExitCode);
            Assert.Contains("true or false", invalidBoolean.Error);
        }

        [Fact]
        public void Repository_configuration_loads_max_cpu_count()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.Write(".pnfmt", "{\n  \"maxCpuCount\": 4\n}\n");

                var configuration = PNFmtConfiguration.Load(directory.Path);
                var commandLineOverride = CommandLineOptions.Parse(new[] { "-m:2" });

                Assert.Equal(4, configuration.MaxCpuCount);
                Assert.Equal(2, commandLineOverride.MaxCpuCount);
                Assert.Null(CommandLineOptions.Parse(Array.Empty<string>()).MaxCpuCount);
            }
        }

        [Theory]
        [InlineData("[]", "root value")]
        [InlineData("{\"maxCpuCount\":0}", "positive integer")]
        [InlineData("{\"threads\":4}", "Unknown setting")]
        [InlineData("not-json", "Unable to read")]
        public void Invalid_repository_configuration_is_rejected(
            string contents,
            string expectedMessage)
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.Write(".pnfmt", contents);

                var exception = Assert.Throws<PNFmtConfigurationException>(
                    () => PNFmtConfiguration.Load(directory.Path));

                Assert.Contains(expectedMessage, exception.Message);
            }
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("1.5")]
        [InlineData("2147483648")]
        [InlineData("\"4\"")]
        [InlineData("true")]
        [InlineData("false")]
        [InlineData("null")]
        [InlineData("[]")]
        [InlineData("{}")]
        public void Invalid_repository_configuration_is_reported_by_the_command(string value)
        {
            using (var directory = new TemporaryDirectory())
            {
                Repository.Init(directory.Path);
                directory.Write(".pnfmt", "{\"maxCpuCount\":" + value + "}");

                var result = RunInDirectory(directory.Path);

                Assert.Equal(2, result.ExitCode);
                Assert.Contains("Invalid PNFmt configuration", result.Error);
                Assert.Contains("positive integer", result.Error);
            }
        }

        [Fact]
        public void Invalid_path_syntax_is_reported_instead_of_crashing()
        {
            var result = Run("invalid\0path");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Unable to access path", result.Error);
            Assert.Contains("No supported files found.", result.Output);
        }

        [Fact]
        public void Path_errors_remain_errors_when_another_file_succeeds()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                var file = directory.WriteResx("valid.resx", "b", "a");
                var wrongExtension = directory.Write("not-supported.txt", "text");
                var missing = Path.Combine(directory.Path, "missing.resx");

                var result = Run(file, missing, wrongExtension);

                Assert.Equal(2, result.ExitCode);
                Assert.Contains("Path not found", result.Error);
                Assert.Contains("supported file type", result.Error);
                Assert.Equal(new[] { "a", "b" }, TemporaryDirectory.ReadNames(file));
            }
        }

        [Fact]
        public void Dry_run_succeeds_without_writing()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                var file = directory.WriteResx("dry.resx", "b", "a");
                var original = File.ReadAllText(file);

                var result = Run("--dry-run", file);

                Assert.Equal(0, result.ExitCode);
                Assert.Equal(original, File.ReadAllText(file));
                Assert.Contains("Would update 1", result.Output);
            }
        }

        [Fact]
        public void Max_cpu_count_matches_msbuild_syntax_and_semantics()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                var first = directory.Write("first.ini", "z=2\na=1\n");
                var second = directory.Write("second.ini", "y=2\nb=1\n");

                var result = Run("-m:2", first, second);
                var longSyntax = Run("-maxCpuCount:1", first, second);
                var processorCount = Run("-m", first, second);
                var zero = Run("-m:0", first);
                var removedOption = Run("--threads", "2", first);

                Assert.Equal(0, result.ExitCode);
                Assert.Equal(0, longSyntax.ExitCode);
                Assert.Equal(0, processorCount.ExitCode);
                Assert.Contains("Processed 2 file(s) in ", result.Output);
                Assert.Equal("a = 1\nz = 2\n", File.ReadAllText(first));
                Assert.Equal("b = 1\ny = 2\n", File.ReadAllText(second));
                Assert.Equal(2, zero.ExitCode);
                Assert.Contains("positive integer", zero.Error);
                Assert.Equal(2, removedOption.ExitCode);
                Assert.Contains("Unknown option", removedOption.Error);
            }
        }

        [Fact]
        public void Default_output_is_one_summary_line_and_verbose_output_lists_files()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                var first = directory.Write("first.ini", "z=2\na=1\n");
                var second = directory.Write("second.ini", "y=2\nb=1\n");

                var normal = Run(first, second);
                var verbose = Run("--verbose", first, second);

                Assert.Single(
                    normal.Output.Split(
                        new[] { "\r\n", "\n" },
                        StringSplitOptions.RemoveEmptyEntries));
                Assert.Contains("Processed 2 file(s) in ", normal.Output);
                Assert.DoesNotContain("first.ini", normal.Output);
                Assert.Contains("[unchanged]", verbose.Output);
                Assert.Contains("first.ini", verbose.Output);
                Assert.Contains("Processed 2 file(s) in ", verbose.Output);
            }
        }

        [Fact]
        public void Legacy_settings_warn_without_verbose_and_pnfmt_settings_win()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.Write(
                    ".editorconfig",
                    "root = true\r\n\r\n"
                    + "[*.csproj]\r\n"
                    + "csproj_formatter_sort_entries = true\r\n"
                    + "pnfmt_sort_entries = false\r\n");
                var project = directory.Write(
                    "Project.csproj",
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"
                    + "<Zeta>z</Zeta><Alpha>a</Alpha></PropertyGroup></Project>");
                var result = Run(project);
                var formatted = File.ReadAllText(project);

                Assert.Equal(0, result.ExitCode);
                Assert.True(
                    formatted.IndexOf("<Zeta>", StringComparison.Ordinal)
                    < formatted.IndexOf("<Alpha>", StringComparison.Ordinal));
                Assert.Contains("warning PNFMT001", result.Error);
                Assert.Contains("csproj_formatter_sort_entries", result.Error);
                Assert.Contains("pnfmt_sort_entries", result.Error);
                Assert.Contains("deprecated and ignored", result.Error);
            }
        }

        [Fact]
        public void Duplicate_targets_are_processed_once()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                var file = directory.WriteResx("duplicate.resx", "b", "a");

                var result = Run(file, file);

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Processed 1 file(s)", result.Output);
            }
        }

        [Fact]
        public void Recursive_option_is_required_for_nested_files()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                var nestedFile = directory.WriteResx(Path.Combine("nested", "nested.resx"), "b", "a");
                var original = File.ReadAllText(nestedFile);

                var nonRecursive = Run(directory.Path);
                Assert.Equal(original, File.ReadAllText(nestedFile));
                var recursive = Run("--recursive", directory.Path);

                Assert.Equal(0, nonRecursive.ExitCode);
                Assert.Contains("Processed 1 file(s)", nonRecursive.Output);
                Assert.Equal(0, recursive.ExitCode);
                Assert.Equal(new[] { "a", "b" }, TemporaryDirectory.ReadNames(nestedFile));
            }
        }

        [Fact]
        public void File_pattern_limits_directory_discovery()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                var project = directory.Write(
                    Path.Combine("nested", "Project.csproj"),
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"
                    + "<Zeta>z</Zeta><Alpha>a</Alpha></PropertyGroup></Project>");
                var resource = directory.WriteResx(
                    Path.Combine("nested", "Strings.resx"),
                    "b",
                    "a");
                var originalResource = File.ReadAllText(resource);

                var result = Run(
                    "--recursive",
                    "--file-pattern",
                    "nested/**/*.csproj",
                    directory.Path);

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Processed 1 file(s)", result.Output);
                Assert.Contains("Updated 1", result.Output);
                Assert.True(
                    File.ReadAllText(project).IndexOf("<Alpha>", StringComparison.Ordinal)
                    < File.ReadAllText(project).IndexOf("<Zeta>", StringComparison.Ordinal));
                Assert.Equal(originalResource, File.ReadAllText(resource));
            }
        }

        [Fact]
        public void Formatter_option_limits_active_formatters()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                var project = directory.Write(
                    "Project.csproj",
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"
                    + "<Zeta>z</Zeta><Alpha>a</Alpha></PropertyGroup></Project>");
                var resource = directory.WriteResx("Strings.resx", "b", "a");
                var originalResource = File.ReadAllText(resource);

                var result = Run("--formatter", "csproj", directory.Path);

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Processed 1 file(s)", result.Output);
                Assert.Contains("Updated 1", result.Output);
                Assert.True(
                    File.ReadAllText(project).IndexOf("<Alpha>", StringComparison.Ordinal)
                    < File.ReadAllText(project).IndexOf("<Zeta>", StringComparison.Ordinal));
                Assert.Equal(originalResource, File.ReadAllText(resource));
            }
        }

        [Fact]
        public void Formatter_option_skips_explicit_files_for_inactive_formatters()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                var resource = directory.WriteResx("Strings.resx", "b", "a");
                var originalResource = File.ReadAllText(resource);

                var result = Run("--formatter", "csproj", resource);

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("No supported files found.", result.Output);
                Assert.Equal(originalResource, File.ReadAllText(resource));
            }
        }

        [Fact]
        public void Formatter_option_rejects_unknown_names()
        {
            var result = Run("--formatter", "not-a-formatter");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Unknown formatter", result.Error);
            Assert.Contains("csproj", result.Error);
        }

        [Fact]
        public void Inactive_and_malformed_files_have_distinct_outcomes()
        {
            using (var inactiveDirectory = new TemporaryDirectory())
            using (var malformedDirectory = new TemporaryDirectory())
            {
                var inactive = inactiveDirectory.WriteResx("inactive.resx", "b", "a");
                var malformed = malformedDirectory.Write("malformed.resx", "<root>");
                malformedDirectory.EnableFormatting();

                var inactiveResult = Run(inactive);
                var malformedResult = Run("--verbose", malformed);

                Assert.Equal(0, inactiveResult.ExitCode);
                Assert.Contains("skipped", inactiveResult.Output);
                Assert.Equal(2, malformedResult.ExitCode);
                Assert.Contains("failed", malformedResult.Output);
                Assert.Contains("Processed 1 file(s) in ", malformedResult.Output);
                Assert.Contains("System.Xml.XmlException", malformedResult.Error);
                Assert.Equal("<root>", File.ReadAllText(malformed));
            }
        }

        [Fact]
        public void Ini_rsp_and_slnx_files_are_skipped_without_explicit_settings()
        {
            using (var directory = new TemporaryDirectory())
            {
                var ini = directory.Write("settings.ini", "z=2\na=1\n");
                var slnx = directory.Write(
                    "Solution.slnx",
                    "<Solution><Project Path=\"Z.csproj\" />"
                    + "<Project Path=\"A.csproj\" /></Solution>");
                var rsp = directory.Write("compiler.rsp", "zeta.cs\nalpha.cs\n");
                var originalIni = File.ReadAllText(ini);
                var originalRsp = File.ReadAllText(rsp);
                var originalSlnx = File.ReadAllText(slnx);

                var result = Run("--verbose", ini, rsp, slnx);

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("skipped 3", result.Output);
                Assert.Equal(originalIni, File.ReadAllText(ini));
                Assert.Equal(originalRsp, File.ReadAllText(rsp));
                Assert.Equal(originalSlnx, File.ReadAllText(slnx));
            }
        }

        [Fact]
        public void Option_terminator_allows_a_dash_prefixed_file_name()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                var file = directory.WriteResx("-input.resx", "b", "a");
                var originalDirectory = Environment.CurrentDirectory;
                try
                {
                    Environment.CurrentDirectory = directory.Path;
                    var result = Run("--", "-input.resx");

                    Assert.Equal(0, result.ExitCode);
                    Assert.Equal(new[] { "a", "b" }, TemporaryDirectory.ReadNames(file));
                }
                finally
                {
                    Environment.CurrentDirectory = originalDirectory;
                }
            }
        }

        [Fact]
        public void Lint_reports_diagnostics_without_editorconfig()
        {
            using (var directory = new TemporaryDirectory())
            {
                var project = directory.Write(
                    "LintProject.csproj",
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"
                    + "<TargetFramework>net10.0</TargetFramework>"
                    + "<TargetFrameworks>net10.0;net9.0</TargetFrameworks>"
                    + "</PropertyGroup><ItemGroup /></Project>");

                var result = Run("--lint", project);

                Assert.Equal(1, result.ExitCode);
                Assert.Contains("warning CSPROJ001", result.Output);
                Assert.Contains("warning CSPROJ004", result.Output);
            }
        }

        [Fact]
        public void Recursive_processing_skips_generated_directories()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.EnableFormatting();
                directory.Write(
                    "Real.csproj",
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"
                    + "<Zeta>z</Zeta><Alpha>a</Alpha></PropertyGroup></Project>");
                var generated = directory.Write(
                    Path.Combine("bin", "Debug", "Generated.csproj"),
                    "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"
                    + "<Zeta>z</Zeta><Alpha>a</Alpha></PropertyGroup></Project>");

                var result = Run("--verbose", "--recursive", directory.Path);

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Real.csproj", result.Output);
                Assert.DoesNotContain("Generated.csproj", result.Output);
                Assert.Contains("<Zeta>z</Zeta><Alpha>a</Alpha>", File.ReadAllText(generated));
            }
        }

        private static (int ExitCode, string Output, string Error) Run(params string[] args)
        {
            return RunWithInput(null, args);
        }

        private static (int ExitCode, string Output, string Error) RunInDirectory(
            string directory,
            params string[] args)
        {
            var originalDirectory = Environment.CurrentDirectory;
            try
            {
                Environment.CurrentDirectory = directory;
                return Run(args);
            }
            finally
            {
                Environment.CurrentDirectory = originalDirectory;
            }
        }

        private static (int ExitCode, string Output, string Error) RunWithInput(
            string input,
            params string[] args)
        {
            var originalOut = Console.Out;
            var originalError = Console.Error;
            var originalInput = Console.In;
            using (var output = new StringWriter())
            using (var error = new StringWriter())
            using (var inputReader = input is null ? null : new StringReader(input))
            {
                try
                {
                    Console.SetOut(output);
                    Console.SetError(error);
                    if (inputReader is not null)
                    {
                        Console.SetIn(inputReader);
                    }

                    var exitCode = Program.Main(args);
                    return (exitCode, output.ToString(), error.ToString());
                }
                finally
                {
                    Console.SetOut(originalOut);
                    Console.SetError(originalError);
                    Console.SetIn(originalInput);
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
                    DefaultEditorConfigDocument.Update(string.Empty));
            }

            public static string[] ReadNames(string path)
            {
                var document = XDocument.Load(path);
                return document.Root
                    .Elements("data")
                    .Select(element => (string)element.Attribute("name"))
                    .ToArray();
            }

            public string WriteResx(string relativePath, params string[] names)
            {
                var entries = string.Join(
                    string.Empty,
                    names.Select(name => $"<data name=\"{name}\"><value>{name}</value></data>"));
                return this.Write(
                    relativePath,
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?><root>"
                    + "<resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>"
                    + entries
                    + "</root>");
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
                    foreach (var file in Directory.GetFiles(
                        this.Path,
                        "*",
                        SearchOption.AllDirectories))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }

                    Directory.Delete(this.Path, true);
                }
            }
        }
    }
}
