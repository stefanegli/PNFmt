// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace PNFmt
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml;
    using System.Xml.Linq;

    internal static class ProjectLinter
    {
        public static IReadOnlyList<FormatterDiagnostic> Analyze(XDocument document, string projectPath)
        {
            var diagnostics = new List<FormatterDiagnostic>();
            if (document.Root is null)
            {
                return diagnostics;
            }

            AnalyzeTopLevelElements(document.Root, diagnostics);
            AnalyzePropertyGroups(document.Root, diagnostics);
            AnalyzeItemGroups(document.Root, diagnostics);
            AnalyzeDefaultItemIncludes(document.Root, projectPath, diagnostics);
            return diagnostics;
        }

        private static void AnalyzeDefaultItemIncludes(
            XElement project,
            string projectPath,
            List<FormatterDiagnostic> diagnostics)
        {
            if (!UsesMicrosoftNetSdk(project) || IsPropertyDisabled(project, "EnableDefaultItems"))
            {
                return;
            }

            var defaultItemTypes = new[]
            {
                new { ItemType = "Compile", DisableProperty = "EnableDefaultCompileItems", Extension = ".cs" },
                new { ItemType = "EmbeddedResource", DisableProperty = "EnableDefaultEmbeddedResourceItems", Extension = ".resx" },
                new { ItemType = "None", DisableProperty = "EnableDefaultNoneItems", Extension = (string)null },
            };

            foreach (var defaultItemType in defaultItemTypes)
            {
                if (IsPropertyDisabled(project, defaultItemType.DisableProperty))
                {
                    continue;
                }

                foreach (var item in project
                    .Elements()
                    .Where(e => e.Name.LocalName == "ItemGroup")
                    .Elements()
                    .Where(e => string.Equals(
                        e.Name.LocalName,
                        defaultItemType.ItemType,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    var include = (string)item.Attribute("Include");
                    if (!IsLocalDefaultItemInclude(include, defaultItemType.Extension))
                    {
                        continue;
                    }

                    if (!HasMatchingDefaultItem(projectPath, include))
                    {
                        continue;
                    }

                    diagnostics.Add(Create(
                        "CSPROJ005",
                        $"{defaultItemType.ItemType} Include '{include}' may duplicate an item implicitly included by the .NET SDK.",
                        item));
                }
            }
        }

        private static void AnalyzeItemGroups(
            XElement project,
            List<FormatterDiagnostic> diagnostics)
        {
            foreach (var itemGroup in project.Elements().Where(e => e.Name.LocalName == "ItemGroup"))
            {
                var items = itemGroup.Elements().ToList();
                if (items.Count == 0)
                {
                    diagnostics.Add(Create(
                        "CSPROJ001",
                        "Empty ItemGroup can be removed.",
                        itemGroup));
                    continue;
                }

                var itemTypes = items
                    .Select(e => e.Name.LocalName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (itemTypes.Count > 1)
                {
                    diagnostics.Add(Create(
                        "CSPROJ002",
                        $"Mixed ItemGroup contains: {string.Join(", ", itemTypes)}.",
                        itemGroup));
                }

                foreach (var duplicate in items
                    .Select(item => new
                    {
                        Item = item,
                        Key = GetDuplicateKey(item),
                    })
                    .Where(x => x.Key != null)
                    .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                    .Where(group => group.Count() > 1))
                {
                    var duplicateItem = duplicate.Skip(1).First().Item;
                    diagnostics.Add(Create(
                        "CSPROJ003",
                        $"Duplicate {duplicateItem.Name.LocalName} item '{GetItemIdentity(duplicateItem)}'.",
                        duplicateItem));
                }
            }
        }

        private static void AnalyzePropertyGroups(
            XElement project,
            List<FormatterDiagnostic> diagnostics)
        {
            foreach (var propertyGroup in project.Elements().Where(e => e.Name.LocalName == "PropertyGroup"))
            {
                if (!propertyGroup.Elements().Any())
                {
                    diagnostics.Add(Create(
                        "CSPROJ001",
                        "Empty PropertyGroup can be removed.",
                        propertyGroup));
                    continue;
                }

                var names = new HashSet<string>(
                    propertyGroup.Elements().Select(e => e.Name.LocalName),
                    StringComparer.OrdinalIgnoreCase);
                if (names.Contains("TargetFramework") && names.Contains("TargetFrameworks"))
                {
                    diagnostics.Add(Create(
                        "CSPROJ004",
                        "PropertyGroup defines both TargetFramework and TargetFrameworks.",
                        propertyGroup));
                }
            }
        }

        private static void AnalyzeTopLevelElements(
            XElement project,
            List<FormatterDiagnostic> diagnostics)
        {
            foreach (var element in project.Elements())
            {
                if (!ProjectStructure.KnownTopLevelElements.Contains(element.Name.LocalName))
                {
                    diagnostics.Add(Create(
                        "CSPROJ006",
                        $"Unexpected top-level element '{element.Name.LocalName}'.",
                        element));
                }
            }
        }

        private static FormatterDiagnostic Create(string code, string message, XObject source)
        {
            var lineInfo = source as IXmlLineInfo;
            var lineNumber = lineInfo != null && lineInfo.HasLineInfo()
                ? (int?)lineInfo.LineNumber
                : null;
            return new FormatterDiagnostic(code, message, lineNumber);
        }

        private static string GetDuplicateKey(XElement item)
        {
            var identity = GetItemIdentity(item);
            if (string.IsNullOrWhiteSpace(identity))
            {
                return null;
            }

            var operation = item.Attribute("Include") != null
                ? "Include"
                : item.Attribute("Update") != null
                    ? "Update"
                    : item.Attribute("Remove") != null
                        ? "Remove"
                        : string.Empty;
            var condition = (string)item.Attribute("Condition") ?? string.Empty;
            return item.Name.LocalName + "\0" + operation + "\0" + identity + "\0" + condition;
        }

        private static string GetItemIdentity(XElement item)
        {
            return (string)item.Attribute("Include")
                ?? (string)item.Attribute("Update")
                ?? (string)item.Attribute("Remove");
        }

        private static bool HasMatchingDefaultItem(
            string projectPath,
            string include)
        {
            var projectDirectory = Path.GetDirectoryName(projectPath) ?? string.Empty;
            var normalizedInclude = include
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            var wildcardIndex = normalizedInclude.IndexOfAny(new[] { '*', '?' });
            if (wildcardIndex < 0)
            {
                return File.Exists(Path.Combine(projectDirectory, normalizedInclude));
            }

            try
            {
                var segments = normalizedInclude.Split(Path.DirectorySeparatorChar)
                    .Where(segment => segment.Length > 0 && segment != ".")
                    .ToArray();
                return HasMatchingFile(projectDirectory, segments, 0);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool HasMatchingFile(string directory, string[] segments, int index)
        {
            if (!Directory.Exists(directory) || index >= segments.Length)
            {
                return false;
            }

            var segment = segments[index];
            if (index == segments.Length - 1)
            {
                return Directory.EnumerateFiles(directory)
                    .Any(file => MatchesSegment(Path.GetFileName(file), segment));
            }

            if (segment == "**" && HasMatchingFile(directory, segments, index + 1))
            {
                return true;
            }

            if (segment.IndexOfAny(new[] { '*', '?' }) < 0)
            {
                return HasMatchingFile(Path.Combine(directory, segment), segments, index + 1);
            }

            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                if (MatchesSegment(Path.GetFileName(child), segment)
                    && HasMatchingFile(child, segments, segment == "**" ? index : index + 1))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsLocalDefaultItemInclude(string include, string expectedExtension)
        {
            if (string.IsNullOrWhiteSpace(include)
                || include.IndexOf("$(", StringComparison.Ordinal) >= 0
                || include.IndexOf("@(", StringComparison.Ordinal) >= 0
                || include.IndexOf("%(", StringComparison.Ordinal) >= 0
                || include.IndexOf(';') >= 0
                || Path.IsPathRooted(include)
                || include.Equals("..", StringComparison.Ordinal)
                || include.StartsWith("../", StringComparison.Ordinal)
                || include.StartsWith(@"..\", StringComparison.Ordinal))
            {
                return false;
            }

            if (expectedExtension != null)
            {
                return include.EndsWith(expectedExtension, StringComparison.OrdinalIgnoreCase);
            }

            return include.IndexOfAny(new[] { '*', '?' }) < 0
                && !include.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                && !include.EndsWith(".resx", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPropertyDisabled(XElement project, string propertyName)
        {
            return project
                .Elements()
                .Where(e => e.Name.LocalName == "PropertyGroup")
                .Elements()
                .Any(property => string.Equals(
                        property.Name.LocalName,
                        propertyName,
                        StringComparison.OrdinalIgnoreCase)
                    && string.Equals(property.Value.Trim(), "false", StringComparison.OrdinalIgnoreCase));
        }

        private static bool MatchesSegment(string name, string pattern)
        {
            // Dynamic programming bounds wildcard matching to O(name * pattern),
            // avoiding the backtracking cost of repeated stars in regex globs.
            var matches = new bool[name.Length + 1];
            matches[0] = true;
            foreach (var character in pattern)
            {
                if (character == '*')
                {
                    for (var index = 1; index <= name.Length; index++)
                    {
                        matches[index] |= matches[index - 1];
                    }
                }
                else
                {
                    for (var index = name.Length; index > 0; index--)
                    {
                        matches[index] = matches[index - 1]
                            && (character == '?' || char.ToUpperInvariant(character) == char.ToUpperInvariant(name[index - 1]));
                    }

                    matches[0] = false;
                }
            }

            return matches[name.Length];
        }

        private static bool UsesMicrosoftNetSdk(XElement project)
        {
            var sdk = (string)project.Attribute("Sdk");
            if (!string.IsNullOrWhiteSpace(sdk)
                && sdk.IndexOf("Microsoft.NET.Sdk", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return project.Elements()
                .Where(e => e.Name.LocalName == "Sdk" || e.Name.LocalName == "Import")
                .Select(e => (string)e.Attribute("Name") ?? (string)e.Attribute("Sdk"))
                .Any(value => !string.IsNullOrWhiteSpace(value)
                    && value.IndexOf("Microsoft.NET.Sdk", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
