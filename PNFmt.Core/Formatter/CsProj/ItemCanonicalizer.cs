// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace PNFmt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;

    internal static class ItemCanonicalizer
    {
        private static readonly Dictionary<string, int> AttributeOrder =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Include"] = 0,
                ["Update"] = 0,
                ["Remove"] = 0,
                ["Exclude"] = 1,
                ["Version"] = 10,
                ["Value"] = 11,
                ["HintPath"] = 20,
                ["Link"] = 21,
                ["LinkBase"] = 22,
                ["DependentUpon"] = 23,
                ["Generator"] = 24,
                ["LastGenOutput"] = 25,
                ["LogicalName"] = 26,
                ["TargetPath"] = 27,
                ["CopyToOutputDirectory"] = 30,
                ["CopyToPublishDirectory"] = 31,
                ["Pack"] = 40,
                ["PackagePath"] = 41,
                ["IncludeAssets"] = 50,
                ["ExcludeAssets"] = 51,
                ["PrivateAssets"] = 52,
                ["Aliases"] = 60,
                ["SpecificVersion"] = 61,
                ["Private"] = 62,
                ["Condition"] = 1000,
            };

        private static readonly Regex MetadataReferenceRegex =
            new Regex(@"%\((?:[^.)]+\.)?([^)]+)\)", RegexOptions.Compiled);

        public static void Canonicalize(XDocument document, HashSet<string> sortableItemTypes)
        {
            if (document.Root is null)
            {
                return;
            }

            foreach (var itemGroup in document.Root.Elements().Where(e => e.Name.LocalName == "ItemGroup"))
            {
                foreach (var item in itemGroup.Elements())
                {
                    if (sortableItemTypes.Contains("*") || sortableItemTypes.Contains(item.Name.LocalName))
                    {
                        SortAttributes(item);
                        SortMetadata(item);
                    }
                }
            }
        }

        private static void SortAttributes(XElement item)
        {
            var attributes = item.Attributes().ToList();
            // MSBuild evaluates metadata attributes in source order, just like
            // metadata elements. Preserve both backward and forward references.
            if (attributes.Count <= 1
                || attributes.Any(attribute => attribute.Value.IndexOf("%(", StringComparison.Ordinal) >= 0))
            {
                return;
            }

            var sortedAttributes = attributes
                .Select((attribute, index) => new { attribute, index })
                .OrderBy(x => x.attribute.IsNamespaceDeclaration ? -1 : GetOrder(AttributeOrder, x.attribute.Name.LocalName))
                .ThenBy(x => x.attribute.IsNamespaceDeclaration ? x.index : 0)
                .ThenBy(x => x.attribute.IsNamespaceDeclaration ? string.Empty : x.attribute.Name.LocalName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.index)
                .Select(x => x.attribute)
                .ToList();

            item.RemoveAttributes();
            item.Add(sortedAttributes);
        }

        private static void SortMetadata(XElement item)
        {
            var nodes = item.Nodes().ToList();
            var groups = new List<MetadataGroup>();
            var leadingNodes = new List<XNode>();
            foreach (var node in nodes)
            {
                if (node is XElement metadata)
                {
                    groups.Add(new MetadataGroup(metadata, new List<XNode>(leadingNodes), groups.Count));
                    leadingNodes.Clear();
                }
                else
                {
                    leadingNodes.Add(node);
                }
            }

            if (groups.Count <= 1)
            {
                return;
            }

            var dependencies = new OriginalOrderDependencies(groups.Select(group => group.Element.Name.LocalName).ToArray());

            for (var i = 0; i < groups.Count; i++)
            {
                var text = groups[i].Element.Value;
                foreach (var attribute in groups[i].Element.DescendantsAndSelf().Attributes())
                {
                    text += " " + attribute.Value;
                }

                foreach (Match match in MetadataReferenceRegex.Matches(text))
                {
                    dependencies.AddReference(i, match.Groups[1].Value);
                }
            }

            var sortedGroups = dependencies.Sort((left, right) => CompareMetadata(groups[left], groups[right]))
                .Select(index => groups[index]);

            var replacementNodes = new List<XNode>();
            foreach (var group in sortedGroups)
            {
                replacementNodes.AddRange(group.LeadingNodes);
                replacementNodes.Add(group.Element);
            }

            replacementNodes.AddRange(leadingNodes);
            item.ReplaceNodes(replacementNodes);
        }

        private static int CompareMetadata(MetadataGroup left, MetadataGroup right)
        {
            var order = GetOrder(AttributeOrder, left.Element.Name.LocalName)
                .CompareTo(GetOrder(AttributeOrder, right.Element.Name.LocalName));
            if (order != 0)
            {
                return order;
            }

            var nameOrder = StringComparer.OrdinalIgnoreCase.Compare(
                left.Element.Name.LocalName,
                right.Element.Name.LocalName);
            return nameOrder != 0 ? nameOrder : left.OriginalIndex.CompareTo(right.OriginalIndex);
        }

        private static int GetOrder(Dictionary<string, int> order, string name)
        {
            return order.TryGetValue(name, out var value) ? value : 500;
        }

        private sealed class MetadataGroup
        {
            public MetadataGroup(XElement element, List<XNode> leadingNodes, int originalIndex)
            {
                this.Element = element;
                this.LeadingNodes = leadingNodes;
                this.OriginalIndex = originalIndex;
            }

            public XElement Element { get; }

            public List<XNode> LeadingNodes { get; }

            public int OriginalIndex { get; }
        }
    }
}
