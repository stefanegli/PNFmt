// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace CsProjFormatter
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml;
    using System.Xml.Linq;

    public enum FormatterRunResult
    {
        Updated,
        Unchanged,
        SkippedNonSdkStyle,
    }

    public class CsProjFormatter
    {
        public CsProjFormatter(ISettings settings, ILog log)
        {
            this.Log = log;
            this.Settings = settings;
        }

        private ILog Log { get; }
        private ISettings Settings { get; }

        public IReadOnlyList<FormatterDiagnostic> Diagnostics { get; private set; } =
            Array.Empty<FormatterDiagnostic>();

        public bool Run(String projectPath)
        {
            return this.Run(projectPath, true);
        }

        public bool Run(String projectPath, bool writeChanges)
        {
            return this.RunWithResult(projectPath, writeChanges) == FormatterRunResult.Updated;
        }

        public FormatterRunResult RunWithResult(String projectPath, bool writeChanges)
        {
            var originalText = File.ReadAllText(projectPath);
            var document = XDocument.Load(projectPath, LoadOptions.SetLineInfo);
            if (!IsSdkStyleProjectDocument(document))
            {
                this.Diagnostics = Array.Empty<FormatterDiagnostic>();
                var skipReason = "Not an SDK-style project file";
                this.Log.WriteLine($"Update was not required: {skipReason}.");
                return FormatterRunResult.SkippedNonSdkStyle;
            }

            this.Diagnostics = ProjectLinter.Analyze(document, projectPath);

            if (this.Settings.SortEntries)
            {
                if (IsProjectDocument(document))
                {
                    var sortableItemTypes = ItemSortingSettings.Resolve(this.Settings);
                    SortPropertyGroups(document);
                    ItemCanonicalizer.Canonicalize(document, sortableItemTypes);
                    SortItemGroups(document, sortableItemTypes);
                    MoveUnexpectedProjectElementsToEnd(document);
                }
            }

            var formattedText = FormatDocument(document, this.Settings);
            if (!string.Equals(originalText, formattedText, StringComparison.Ordinal))
            {
                if (writeChanges)
                {
                    File.WriteAllText(projectPath, formattedText);
                }

                var action = writeChanges ? "Updating" : "Would update";
                this.Log.WriteLine($"{action} {projectPath}");
                return FormatterRunResult.Updated;
            }

            var reason = "No modifications";
            this.Log.WriteLine($"Update was not required: {reason}.");
            return FormatterRunResult.Unchanged;
        }

        private static string FormatDocument(XDocument document, ISettings settings)
        {
            var indentChars = settings.ResolveIndentChars();
            var newLineChars = settings.ResolveNewLineChars();
            var writerSettings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = indentChars,
                NewLineChars = newLineChars,
                NewLineHandling = NewLineHandling.Replace,
                OmitXmlDeclaration = document.Declaration is null,
            };

            using (var stringWriter = new StringWriter())
            using (var xmlWriter = XmlWriter.Create(stringWriter, writerSettings))
            {
                document.Save(xmlWriter);
                xmlWriter.Flush();
                return ApplyTopLevelGroupSpacing(stringWriter.ToString(), settings, newLineChars, indentChars);
            }
        }

        private static string ApplyTopLevelGroupSpacing(
            string formattedText,
            ISettings settings,
            string newLineChars,
            string indentChars)
        {
            if (settings.EmptyLinesBetweenGroups <= 0 || string.IsNullOrEmpty(formattedText))
            {
                return formattedText;
            }

            var lines = formattedText.Split(new[] { newLineChars }, StringSplitOptions.None);
            if (lines.Length < 3)
            {
                return formattedText;
            }

            var outputLines = new List<string>(lines.Length + 16);
            for (var i = 0; i < lines.Length; i++)
            {
                outputLines.Add(lines[i]);
                if (i >= lines.Length - 1)
                {
                    continue;
                }

                if (ShouldSeparateTopLevelGroups(lines[i], lines[i + 1], indentChars))
                {
                    for (var j = 0; j < settings.EmptyLinesBetweenGroups; j++)
                    {
                        outputLines.Add(string.Empty);
                    }
                }
            }

            return string.Join(newLineChars, outputLines);
        }

        private static bool ShouldSeparateTopLevelGroups(string currentLine, string nextLine, string indentChars)
        {
            if (!TryGetTopLevelTagText(currentLine, indentChars, out var currentTagText))
            {
                return false;
            }

            if (!TryGetTopLevelTagText(nextLine, indentChars, out var nextTagText))
            {
                return false;
            }

            if (!(currentTagText.StartsWith("</", StringComparison.Ordinal) || currentTagText.EndsWith("/>", StringComparison.Ordinal)))
            {
                return false;
            }

            if (!nextTagText.StartsWith("<", StringComparison.Ordinal) || nextTagText.StartsWith("</", StringComparison.Ordinal))
            {
                return false;
            }

            if (nextTagText.StartsWith("<?", StringComparison.Ordinal) || nextTagText.StartsWith("<!--", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        private static bool TryGetTopLevelTagText(string line, string indentChars, out string tagText)
        {
            tagText = null;
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            if (!line.StartsWith(indentChars, StringComparison.Ordinal))
            {
                return false;
            }

            if (line.StartsWith(indentChars + indentChars, StringComparison.Ordinal))
            {
                return false;
            }

            tagText = line.Substring(indentChars.Length).Trim();
            return tagText.Length > 0;
        }

        private static bool IsProjectDocument(XDocument document)
        {
            return document.Root?.Name.LocalName == "Project";
        }

        private static bool IsSdkStyleProjectDocument(XDocument document)
        {
            if (!IsProjectDocument(document) || document.Root is null)
            {
                return false;
            }

            var root = document.Root;
            if (HasAttributeValue(root, "Sdk"))
            {
                return true;
            }

            if (root.Elements().Any(e => e.Name.LocalName == "Sdk"))
            {
                return true;
            }

            return root.Elements()
                .Where(e => e.Name.LocalName == "Import")
                .Any(e => HasAttributeValue(e, "Sdk"));
        }

        private static bool HasAttributeValue(XElement element, string attributeLocalName)
        {
            var attribute = element.Attributes()
                .FirstOrDefault(a => a.Name.LocalName == attributeLocalName);
            return attribute != null && !string.IsNullOrWhiteSpace(attribute.Value);
        }

        private static List<ElementGroup> SortElementGroupsWithDependencies(List<ElementGroup> groups)
        {
            if (groups.Count <= 1)
            {
                return groups;
            }

            var comparer = StringComparer.OrdinalIgnoreCase;
            var nameToIndices = new Dictionary<string, List<int>>(comparer);
            for (var i = 0; i < groups.Count; i++)
            {
                var name = groups[i].Element.Name.LocalName;
                if (!nameToIndices.TryGetValue(name, out var indices))
                {
                    indices = new List<int>();
                    nameToIndices.Add(name, indices);
                }

                indices.Add(i);
            }

            var edges = new List<HashSet<int>>(groups.Count);
            var indegree = new int[groups.Count];
            for (var i = 0; i < groups.Count; i++)
            {
                edges.Add(new HashSet<int>());
            }

            var referenceRegex = new Regex(@"\$\(([^)]+)\)", RegexOptions.Compiled);
            for (var i = 0; i < groups.Count; i++)
            {
                var element = groups[i].Element;
                var text = element.Value;
                foreach (var attribute in element.Attributes())
                {
                    text += " " + attribute.Value;
                }

                foreach (Match match in referenceRegex.Matches(text))
                {
                    if (match.Groups.Count < 2)
                    {
                        continue;
                    }

                    var referenceName = match.Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(referenceName))
                    {
                        continue;
                    }

                    if (!nameToIndices.TryGetValue(referenceName, out var indices))
                    {
                        continue;
                    }

                    foreach (var referencedIndex in indices)
                    {
                        if (referencedIndex == i)
                        {
                            continue;
                        }

                        // MSBuild expands properties in document order. Preserve the
                        // original relative order of a reference and every assignment
                        // to the referenced property instead of "fixing" a forward
                        // reference and thereby changing its evaluated value.
                        var earlierIndex = Math.Min(referencedIndex, i);
                        var laterIndex = Math.Max(referencedIndex, i);
                        if (edges[earlierIndex].Add(laterIndex))
                        {
                            indegree[laterIndex]++;
                        }
                    }
                }
            }

            // Repeated assignments are also order-sensitive because the last
            // assignment wins.
            foreach (var indices in nameToIndices.Values)
            {
                for (var i = 1; i < indices.Count; i++)
                {
                    var previousIndex = indices[i - 1];
                    var currentIndex = indices[i];
                    if (edges[previousIndex].Add(currentIndex))
                    {
                        indegree[currentIndex]++;
                    }
                }
            }

            var ready = new List<int>();
            for (var i = 0; i < indegree.Length; i++)
            {
                if (indegree[i] == 0)
                {
                    ready.Add(i);
                }
            }

            var result = new List<ElementGroup>(groups.Count);
            while (ready.Count > 0)
            {
                ready.Sort((left, right) =>
                {
                    var leftName = groups[left].Element.Name.LocalName;
                    var rightName = groups[right].Element.Name.LocalName;
                    var nameCompare = comparer.Compare(leftName, rightName);
                    return nameCompare != 0 ? nameCompare : left.CompareTo(right);
                });

                var next = ready[0];
                ready.RemoveAt(0);
                result.Add(groups[next]);

                foreach (var dependent in edges[next])
                {
                    indegree[dependent]--;
                    if (indegree[dependent] == 0)
                    {
                        ready.Add(dependent);
                    }
                }
            }

            if (result.Count == groups.Count)
            {
                return result;
            }

            var remaining = new List<int>();
            for (var i = 0; i < groups.Count; i++)
            {
                if (!result.Contains(groups[i]))
                {
                    remaining.Add(i);
                }
            }

            remaining.Sort((left, right) =>
            {
                var leftName = groups[left].Element.Name.LocalName;
                var rightName = groups[right].Element.Name.LocalName;
                var nameCompare = comparer.Compare(leftName, rightName);
                return nameCompare != 0 ? nameCompare : left.CompareTo(right);
            });

            foreach (var index in remaining)
            {
                result.Add(groups[index]);
            }

            return result;
        }

        private static void SortPropertyGroups(XDocument document)
        {
            foreach (var propertyGroup in document.Root.Elements().Where(e => e.Name.LocalName == "PropertyGroup"))
            {
                var nodes = propertyGroup.Nodes().ToList();
                var groups = new List<ElementGroup>();
                var leadingNodes = new List<XNode>();

                foreach (var node in nodes)
                {
                    if (node is XElement element)
                    {
                        groups.Add(new ElementGroup(element, new List<XNode>(leadingNodes)));
                        leadingNodes.Clear();
                    }
                    else
                    {
                        leadingNodes.Add(node);
                    }
                }

                var trailingNodes = new List<XNode>(leadingNodes);
                if (groups.Count == 0)
                {
                    continue;
                }

                var sortedGroups = SortElementGroupsWithDependencies(groups);

                var newNodes = new List<XNode>();
                foreach (var group in sortedGroups)
                {
                    newNodes.AddRange(group.LeadingNodes);
                    newNodes.Add(group.Element);
                }

                newNodes.AddRange(trailingNodes);
                propertyGroup.ReplaceNodes(newNodes);
            }
        }

        private static void MoveUnexpectedProjectElementsToEnd(XDocument document)
        {
            if (document.Root is null)
            {
                return;
            }

            var keptNodes = new List<XNode>();
            var unexpectedElements = new List<XElement>();

            foreach (var node in document.Root.Nodes())
            {
                if (node is XElement element)
                {
                    if (ProjectStructure.KnownTopLevelElements.Contains(element.Name.LocalName))
                    {
                        keptNodes.Add(element);
                    }
                    else
                    {
                        unexpectedElements.Add(element);
                    }
                }
                else
                {
                    keptNodes.Add(node);
                }
            }

            if (unexpectedElements.Count == 0)
            {
                return;
            }

            keptNodes.AddRange(unexpectedElements);
            document.Root.ReplaceNodes(keptNodes);
        }

        private static void SortItemGroups(XDocument document, HashSet<string> sortableItemTypes)
        {
            foreach (var itemGroup in document.Root.Elements().Where(e => e.Name.LocalName == "ItemGroup"))
            {
                var elementNames = itemGroup.Elements()
                    .Select(e => e.Name.LocalName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (elementNames.Count != 1)
                {
                    continue;
                }

                var elementName = elementNames[0];
                if (!sortableItemTypes.Contains("*") && !sortableItemTypes.Contains(elementName))
                {
                    continue;
                }

                if (!CanSafelySortItemGroup(itemGroup, elementName))
                {
                    continue;
                }

                switch (elementName)
                {
                    case "PackageReference":
                        SortPackageReferencesInGroup(itemGroup);
                        break;
                    case "ProjectReference":
                    case "Reference":
                    default:
                        SortItemGroupElements(itemGroup, elementName);
                        break;
                }
            }
        }

        private static bool CanSafelySortItemGroup(XElement itemGroup, string elementName)
        {
            var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var element in itemGroup.Elements().Where(e => e.Name.LocalName == elementName))
            {
                var include = (string)element.Attribute("Include");
                if (string.IsNullOrWhiteSpace(include)
                    || element.Attribute("Update") != null
                    || element.Attribute("Remove") != null
                    || include.IndexOf("@(", StringComparison.Ordinal) >= 0
                    || include.IndexOf("%(", StringComparison.Ordinal) >= 0
                    || !identities.Add(include))
                {
                    return false;
                }
            }

            return true;
        }

        private static void SortPackageReferencesInGroup(XElement itemGroup)
        {
            if (!TryCollectElementGroups(itemGroup, e => e.Name.LocalName == "PackageReference", out var groups, out var trailingNodes))
            {
                return;
            }

            var comparer = StringComparer.OrdinalIgnoreCase;
            var sortedGroups = groups
                .Select((group, index) => new { group, index })
                .OrderBy(x => GetPackageGroupOrder(x.group.Element))
                .ThenBy(x => GetPackageSortKey(x.group.Element), comparer)
                .ThenBy(x => x.index)
                .Select(x => x.group)
                .ToList();

            ReplaceNodes(itemGroup, sortedGroups, trailingNodes);
        }

        private static void SortItemGroupElements(XElement itemGroup, string elementName)
        {
            if (!TryCollectElementGroups(itemGroup, e => e.Name.LocalName == elementName, out var groups, out var trailingNodes))
            {
                return;
            }

            var comparer = StringComparer.OrdinalIgnoreCase;
            var sortedGroups = groups
                .Select((group, index) => new { group, index })
                .OrderBy(x => GetPackageSortKey(x.group.Element), comparer)
                .ThenBy(x => x.index)
                .Select(x => x.group)
                .ToList();

            ReplaceNodes(itemGroup, sortedGroups, trailingNodes);
        }

        private static string GetPackageSortKey(XElement element)
        {
            return (string)element.Attribute("Include")
                ?? (string)element.Attribute("Update")
                ?? element.Name.LocalName
                ?? string.Empty;
        }

        private static int GetPackageGroupOrder(XElement element)
        {
            if (HasCondition(element))
            {
                return 3;
            }

            if (HasMetadata(element, "PrivateAssets"))
            {
                return 2;
            }

            if (HasMetadata(element, "IncludeAssets"))
            {
                return 1;
            }

            return 0;
        }

        private static bool HasCondition(XElement element)
        {
            var condition = element.Attribute("Condition");
            return condition != null && !string.IsNullOrWhiteSpace(condition.Value);
        }

        private static bool HasMetadata(XElement element, string name)
        {
            var attribute = element.Attribute(name);
            if (attribute != null && !string.IsNullOrWhiteSpace(attribute.Value))
            {
                return true;
            }

            var child = element.Elements().FirstOrDefault(e => e.Name.LocalName == name);
            return child != null && !string.IsNullOrWhiteSpace(child.Value);
        }

        private static bool TryCollectElementGroups(
            XElement parent,
            Func<XElement, bool> isTargetElement,
            out List<ElementGroup> groups,
            out List<XNode> trailingNodes)
        {
            var nodes = parent.Nodes().ToList();
            groups = new List<ElementGroup>();
            var leadingNodes = new List<XNode>();

            foreach (var node in nodes)
            {
                if (node is XElement element && isTargetElement(element))
                {
                    groups.Add(new ElementGroup(element, new List<XNode>(leadingNodes)));
                    leadingNodes.Clear();
                }
                else
                {
                    leadingNodes.Add(node);
                }
            }

            trailingNodes = new List<XNode>(leadingNodes);
            return groups.Count > 0;
        }

        private static void ReplaceNodes(XElement parent, List<ElementGroup> sortedGroups, List<XNode> trailingNodes)
        {
            var newNodes = new List<XNode>();
            foreach (var group in sortedGroups)
            {
                newNodes.AddRange(group.LeadingNodes);
                newNodes.Add(group.Element);
            }

            newNodes.AddRange(trailingNodes);
            parent.ReplaceNodes(newNodes);
        }

        private sealed class ElementGroup
        {
            public ElementGroup(XElement element, List<XNode> leadingNodes)
            {
                this.Element = element;
                this.LeadingNodes = leadingNodes;
            }

            public XElement Element { get; }

            public List<XNode> LeadingNodes { get; }
        }
    }
}
