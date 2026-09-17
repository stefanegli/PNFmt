// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using PNFmt.Tests.Formatter.Ini;
using PNFmt.Tests.Formatter.Resx;

namespace PNFmt.Tests.Snapshots
{
    public static class SnapshotReviewExporter
    {
        public static void Write(string repositoryRoot, string outputPath)
        {
            var cases = ReadCases(repositoryRoot);
            var template = File.ReadAllText(Path.Combine(repositoryRoot, "tools", "SnapshotViewer", "viewer.html"));
            var json = JsonSerializer.Serialize(new { generated = DateTimeOffset.UtcNow, cases });
            var html = Regex.Replace(template, @"/\*SNAPSHOT_DATA\*/\s*null", _ => json);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
            File.WriteAllText(outputPath, html, new UTF8Encoding(false));
        }

        internal static IReadOnlyList<ReviewCase> ReadCases(string root)
        {
            var snapshotRoot = Path.Combine(root, "Snapshots", "PNFmt.Tests", "Formatter");
            var results = new List<ReviewCase>();
            foreach (var snapshot in Directory.GetFiles(Path.Combine(root, "Snapshots"), "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
            {
                if (!snapshot.StartsWith(snapshotRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Unmapped snapshot location: " + snapshot);
                }

                var parts = Path.GetRelativePath(snapshotRoot, snapshot).Replace('\\', '/').Split('/');
                var provider = parts[0];
                var testClass = parts[1];
                var testMethod = parts[2];
                var name = string.Join("/", parts.Skip(3));
                var snapshotName = name;
                var fixtureRoot = Path.Combine(root, "PNFmt.Tests", "Formatter", provider);
                string input;
                string inputPath;
                string settings;
                var description = testMethod.Replace('_', ' ');
                if (testClass == nameof(EditorConfigPrecedenceTests))
                {
                    var data = EditorConfigPrecedenceTests.Cases.Single(row => (string)row[0] == Path.GetFileNameWithoutExtension(name));
                    input = "root=true\n\n" + (string)data[1];
                    inputPath = "PNFmt.Tests/Formatter/Ini/EditorConfigPrecedenceTests.cs • inline Cases";
                    settings = "Direct formatter call: sortEntries, sortGroups, groupByPrefix, mergeGroups = true; isEditorConfig = true.\n"
                        + "The test also checks all 16 option combinations; this snapshot records the all-enabled combination.";
                }
                else if (testClass == "DefaultEditorConfigDocumentTests")
                {
                    input = string.Empty;
                    inputPath = "Empty input string";
                    settings = "DefaultEditorConfigDocument.Update(string.Empty)";
                    description = "Generate the default configuration from an empty document.";
                }
                else if (testClass == nameof(ResxSnapshotTests) && testMethod == nameof(ResxSnapshotTests.Files_are_processed_correctly))
                {
                    var data = new ResxSnapshotTests.ResxSnapshotData().Single(row => Path.GetFileNameWithoutExtension((string)row[1]) == Path.GetFileNameWithoutExtension(name));
                    var source = Path.Combine(fixtureRoot, "_files", (string)data[1]);
                    input = File.ReadAllText(source);
                    inputPath = Relative(root, source);
                    description = (string)data[0];
                    var options = (IResxFormatSettings)data[3];
                    settings = $"Direct formatter settings\nSortEntries = {options.SortEntries}\nRemoveXsdSchema = {options.RemoveXsdSchema}\n"
                        + $"RemoveDocumentationComment = {options.RemoveDocumentationComment}\nComparer = {options.Comparer}\nCulture = {data[2] ?? "en-US"}";
                }
                else
                {
                    var inputRoot = Path.Combine(fixtureRoot, "_files", "input");
                    var configurationRoot = inputRoot;
                    if (testClass == nameof(ResxEditorConfigSnapshotTests))
                    {
                        var folder = testMethod switch
                        {
                            "Alternate_sort_method_can_be_configured" => "sort",
                            "Different_file_extensions_can_be_processed" => "filetype",
                            "EditorConfig_files_can_be_specified_per_folder" => parts[3],
                            "Resx_comment_and_schema_are_inserted_if_necessary" => "insertCommentAndSchema",
                            "Resx_comment_is_inserted_if_necessary" => "insertComment",
                            "Xsd_schema_can_be_removed" => "removeXsdSchema",
                            "Formatter_reports_inactive_if_EditorConfig_does_not_enable_it" => "inactive",
                            _ => throw new InvalidOperationException("Unmapped RESX snapshot test: " + testMethod),
                        };
                        inputRoot = Path.Combine(fixtureRoot, "_editor", folder);
                        configurationRoot = Path.Combine(fixtureRoot, "_editor");
                        name = Path.GetFileName(snapshot);
                    }
                    else if (testClass != provider + "SnapshotTests")
                    {
                        throw new InvalidOperationException("Unmapped snapshot class: " + testClass);
                    }

                    var source = Path.Combine(inputRoot, name);
                    input = File.ReadAllText(source);
                    inputPath = Relative(root, source);
                    var config = new List<string>();
                    for (var directory = Path.GetDirectoryName(source); directory is not null; directory = Path.GetDirectoryName(directory))
                    {
                        var configPath = Path.Combine(directory, ".editorconfig");
                        if (File.Exists(configPath))
                        {
                            config.Insert(0, Relative(root, configPath) + "\n" + File.ReadAllText(configPath));
                        }

                        if (directory == configurationRoot)
                        {
                            break;
                        }
                    }

                    settings = config.Count == 0 ? "No fixture EditorConfig file." : string.Join("\n\n", config);
                }

                var label = provider switch
                {
                    "CSharp" => "C#",
                    "CsProj" => "Project",
                    "Ini" or "EditorConfig" => "INI / EditorConfig",
                    "Resx" => "Resource",
                    "Rsp" => "Response",
                    "Slnx" => "Solution",
                    "Xml" => name.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) ? "XAML" : "XML",
                    _ => throw new InvalidOperationException("Unmapped provider: " + provider),
                };
                results.Add(new ReviewCase(Relative(root, snapshot), label, snapshotName, testClass + "." + testMethod,
                    description, inputPath, input, File.ReadAllText(snapshot), settings));
            }

            return results;
        }

        private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');

        internal sealed record ReviewCase(string id, string provider, string name, string test, string description,
            string inputPath, string input, string result, string settings);
    }
}
