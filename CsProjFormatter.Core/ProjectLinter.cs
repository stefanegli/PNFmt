// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace CsProjFormatter
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

                    if (!HasMatchingDefaultItem(projectPath, include, defaultItemType.Extension))
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

        private static bool HasMatchingDefaultItem(
            string projectPath,
            string include,
            string expectedExtension)
        {
            var projectDirectory = Path.GetDirectoryName(projectPath) ?? string.Empty;
            var normalizedInclude = include
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            var wildcardIndex = normalizedInclude.IndexOfAny(new[] { '*', '?' });
            if (wildcardIndex < 0)
            {
                return File.Exists(Path.Combine(projectDirectory, normalizedInclude));
            }

            var prefix = normalizedInclude.Substring(0, wildcardIndex);
            var separatorIndex = prefix.LastIndexOf(Path.DirectorySeparatorChar);
            var searchDirectory = separatorIndex >= 0
                ? Path.Combine(projectDirectory, prefix.Substring(0, separatorIndex))
                : projectDirectory;
            if (!Directory.Exists(searchDirectory))
            {
                return false;
            }

            try
            {
                return Directory
                    .EnumerateFiles(searchDirectory, "*", SearchOption.AllDirectories)
                    .Any(file => expectedExtension != null
                        ? file.EndsWith(expectedExtension, StringComparison.OrdinalIgnoreCase)
                        : !file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                            && !file.EndsWith(".resx", StringComparison.OrdinalIgnoreCase));
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

        private static FormatterDiagnostic Create(string code, string message, XObject source)
        {
            var lineInfo = source as IXmlLineInfo;
            var lineNumber = lineInfo != null && lineInfo.HasLineInfo()
                ? (int?)lineInfo.LineNumber
                : null;
            return new FormatterDiagnostic(code, message, lineNumber);
        }
    }
}
