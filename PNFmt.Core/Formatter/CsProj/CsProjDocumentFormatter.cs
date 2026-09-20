// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace PNFmt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml;
    using System.Xml.Linq;

    internal sealed class CsProjDocumentFormatter
    {
        private static readonly Regex PropertyReferenceRegex =
            new Regex(@"\$\(([^)]+)\)", RegexOptions.Compiled);

        public CsProjDocumentFormatter(ICsProjFormatSettings settings)
        {
            this.Settings = settings;
        }

        private ICsProjFormatSettings Settings { get; }

        public FileFormatResult Run(FileFormatRequest request, bool formatLayout = true)
        {
            return TextFileFormatPipeline.Format(request, true,
                (text, encoding) => this.Format(text, request.FilePath, request.Lint, formatLayout, request.Configuration.Properties, encoding), xml: true);
        }

        private static string ApplyTopLevelGroupSpacing(
            string formattedText,
            ICsProjFormatSettings settings,
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

        private static bool CanSafelySortItemGroup(XElement itemGroup, string elementName)
        {
            var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var element in itemGroup.Elements().Where(e => e.Name.LocalName == elementName))
            {
                var include = (string)element.Attribute("Include");
                if (string.IsNullOrWhiteSpace(include)
                    // Expressions, globs, lists, and escapes can expand to duplicate
                    // identities even when the Include strings differ.
                    || include.IndexOf("$(", StringComparison.Ordinal) >= 0
                    || include.IndexOfAny(new[] { '*', '?', ';', '%' }) >= 0
                    || element.Attribute("Update") != null
                    || element.Attribute("Remove") != null
                    || HasItemReference(element.Value)
                    || element.DescendantsAndSelf().Attributes().Any(attribute => HasItemReference(attribute.Value))
                    || !identities.Add(include))
                {
                    return false;
                }
            }

            return true;
        }

        private DocumentFormatResult Format(string originalText, string projectPath, bool lint, bool formatLayout,
            IReadOnlyDictionary<string, string> properties, Encoding outputEncoding)
        {
            var document = XDocument.Parse(originalText, LoadOptions.SetLineInfo
                | (formatLayout ? LoadOptions.None : LoadOptions.PreserveWhitespace));
            var originalDocument = formatLayout ? null : new XDocument(document);
            if (!IsProjectDocument(document))
            {
                return DocumentFormatResult.Skipped();
            }

            var diagnostics = lint
                ? ProjectLinter.Analyze(document, projectPath)
                : Array.Empty<FormatterDiagnostic>();

            if (this.Settings.SortEntries)
            {
                var sortableItemTypes = CsProjItemSorting.Resolve(this.Settings);
                SortPropertyGroups(document);
                ItemCanonicalizer.Canonicalize(document, sortableItemTypes);
                SortItemGroups(document, sortableItemTypes);
                MoveUnexpectedProjectElementsToEnd(document);
            }

            var formattedText = !formatLayout && XNode.DeepEquals(originalDocument, document)
                ? originalText
                : FormatDocument(document, this.Settings, formatLayout);
            if (formatLayout && document.DocumentType is null)
            {
                formattedText = XmlDocumentFormatter.WrapAttributes(formattedText, properties,
                    this.Settings.ResolveIndentChars(), this.Settings.ResolveNewLineChars(), this.Settings.IndentSize, outputEncoding: outputEncoding);
            }
            return DocumentFormatResult.FromText(formattedText, diagnostics);
        }

        private static string FormatDocument(XDocument document, ICsProjFormatSettings settings, bool formatLayout)
        {
            var indentChars = settings.ResolveIndentChars();
            var newLineChars = settings.ResolveNewLineChars();
            var writerSettings = new XmlWriterSettings
            {
                Indent = formatLayout,
                IndentChars = indentChars,
                NewLineChars = newLineChars,
                NewLineHandling = NewLineHandling.Entitize,
                OmitXmlDeclaration = document.Declaration is null,
            };

            using (var stringWriter = new Utf8StringWriter())
            using (var xmlWriter = XmlWriter.Create(stringWriter, writerSettings))
            {
                document.Save(xmlWriter);
                xmlWriter.Flush();
                if (!formatLayout)
                {
                    return stringWriter.ToString();
                }

                var formatted = ApplyTopLevelGroupSpacing(
                    stringWriter.ToString(),
                    settings,
                    newLineChars,
                    indentChars).TrimEnd('\r', '\n');
                return settings.InsertFinalNewline
                    ? formatted + newLineChars
                    : formatted;
            }
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

        private static string GetPackageSortKey(XElement element)
        {
            return (string)element.Attribute("Include")
                ?? (string)element.Attribute("Update")
                ?? element.Name.LocalName
                ?? string.Empty;
        }

        private static bool HasCondition(XElement element)
        {
            var condition = element.Attribute("Condition");
            return condition != null && !string.IsNullOrWhiteSpace(condition.Value);
        }

        private static bool HasItemReference(string text)
        {
            // Conditions, metadata values, and Exclude can depend on items defined
            // earlier in the group just as Include can.
            return text.IndexOf("@(", StringComparison.Ordinal) >= 0
                || text.IndexOf("%(", StringComparison.Ordinal) >= 0;
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

        private static bool IsProjectDocument(XDocument document)
        {
            return document.Root?.Name.LocalName == "Project";
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

        private static List<ElementGroup> SortElementGroupsWithDependencies(List<ElementGroup> groups)
        {
            if (groups.Count <= 1)
            {
                return groups;
            }

            var dependencies = new OriginalOrderDependencies(groups.Select(group => group.Element.Name.LocalName).ToArray());

            for (var i = 0; i < groups.Count; i++)
            {
                var element = groups[i].Element;
                var text = element.Value;
                foreach (var attribute in element.DescendantsAndSelf().Attributes())
                {
                    text += " " + attribute.Value;
                }

                foreach (Match match in PropertyReferenceRegex.Matches(text))
                {
                    if (match.Groups.Count < 2)
                    {
                        continue;
                    }

                    var referenceName = match.Groups[1].Value;
                    // Property functions, member access, and nested expansions cannot
                    // be resolved by this file-local dependency scan. Preserve the
                    // whole group rather than moving a prerequisite across the expression.
                    if (referenceName.IndexOf('(') >= 0
                        || referenceName.IndexOf('.') >= 0
                        || referenceName.IndexOf('[') >= 0)
                    {
                        return groups;
                    }

                    if (string.IsNullOrWhiteSpace(referenceName))
                    {
                        continue;
                    }

                    dependencies.AddReference(i, referenceName);
                }
            }

            return dependencies.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(
                    groups[left].Element.Name.LocalName, groups[right].Element.Name.LocalName))
                .Select(index => groups[index]).ToList();
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
