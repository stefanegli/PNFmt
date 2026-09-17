// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PNFmt
{
    internal static class SlnxDocumentFormatter
    {
        public static string Format(string text, bool sortEntries = true, bool formatLayout = true)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreWhitespace = false,
                XmlResolver = null,
            };

            XDocument document;
            using (var stringReader = new StringReader(text))
            using (var xmlReader = XmlReader.Create(stringReader, readerSettings))
            {
                document = XDocument.Load(xmlReader, LoadOptions.PreserveWhitespace);
            }

            var root = document.Root;
            if (root is null || root.Name != XName.Get("Solution"))
            {
                throw new InvalidDataException("The file does not contain an SLNX Solution root element.");
            }

            if (!sortEntries && !formatLayout)
            {
                return text;
            }

            var originalDocument = formatLayout ? null : new XDocument(document);
            SortChildren(root, GetSolutionOrder, GetSolutionKey, sortEntries, formatLayout);

            foreach (var configurations in root.Elements("Configurations"))
            {
                SortChildren(configurations, GetConfigurationOrder, GetConfigurationKey, sortEntries, formatLayout);
                foreach (var projectType in configurations.Elements("ProjectType"))
                {
                    SortChildren(projectType, GetProjectRuleOrder, GetProjectRuleKey, sortEntries, formatLayout);
                }
            }

            foreach (var folder in root.Elements("Folder"))
            {
                SortChildren(folder, GetFolderOrder, GetFolderKey, sortEntries, formatLayout);
                SortProperties(folder, sortEntries, formatLayout);
                foreach (var project in folder.Elements("Project"))
                {
                    SortProject(project, sortEntries, formatLayout);
                }
            }

            foreach (var project in root.Elements("Project"))
            {
                SortProject(project, sortEntries, formatLayout);
            }

            SortProperties(root, sortEntries, formatLayout);
            if (!formatLayout && XNode.DeepEquals(originalDocument, document))
            {
                return text;
            }

            var newLine = TextFileFormatting.DetectNewLine(text);
            // Only containers visited by the SLNX sorter own layout whitespace.
            // Serialize without implicit indentation so extension subtrees retain
            // even whitespace-only values and compact element-only content.
            foreach (var container in document.Descendants().Where(element => formatLayout && element.Annotation<LayoutContainer>() is not null).ToArray())
            {
                var nodes = container.Nodes().Where(node => !IsLayoutWhitespace(node)).ToArray();
                if (nodes.Length == 0)
                {
                    continue;
                }

                var indent = new string(' ', container.Ancestors().Count() * 2);
                container.ReplaceNodes(nodes.SelectMany(node => new XNode[] { new LayoutWhitespace(newLine + indent + "  "), node })
                    .Concat(new[] { new LayoutWhitespace(newLine + indent) }).ToArray());
            }

            if (formatLayout)
            {
                var documentNodes = document.Nodes().Where(node => !IsLayoutWhitespace(node)).ToArray();
                document.ReplaceNodes(documentNodes.SelectMany((node, index) =>
                    index > 0 || document.Declaration is not null ? new XNode[] { new LayoutWhitespace(newLine), node } : new[] { node }).ToArray());
            }
            var writerSettings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = false,
                NewLineChars = newLine,
                NewLineHandling = NewLineHandling.Entitize,
                OmitXmlDeclaration = document.Declaration is null,
            };

            using (var writer = new Utf8StringWriter())
            using (var xmlWriter = XmlWriter.Create(writer, writerSettings))
            {
                document.Save(xmlWriter);
                xmlWriter.Flush();
                var formatted = writer.ToString();
                return !formatLayout || formatted.EndsWith(newLine, StringComparison.Ordinal)
                    ? formatted
                    : formatted + newLine;
            }
        }

        private static void AddSortedGroups(
            List<ElementGroup> groups,
            List<XNode> nodes,
            Func<XElement, int> order,
            Func<XElement, string> key)
        {
            foreach (var group in groups
                .OrderBy(item => order(item.Element))
                .ThenBy(item => key(item.Element), StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => key(item.Element), StringComparer.Ordinal))
            {
                nodes.AddRange(group.LeadingNodes);
                nodes.Add(group.Element);
            }

            groups.Clear();
        }

        private static string Attribute(XElement element, string name)
        {
            return (string)element.Attribute(name);
        }

        private static string GetConfigurationKey(XElement element)
        {
            return Attribute(element, "Name")
                ?? Attribute(element, "TypeId")
                ?? Attribute(element, "Extension")
                ?? element.Name.LocalName;
        }

        private static int GetConfigurationOrder(XElement element)
        {
            switch (element.Name.LocalName)
            {
                case "BuildType": return 0;
                case "Platform": return 1;
                case "ProjectType": return 2;
                default: return int.MaxValue;
            }
        }

        private static string GetFolderKey(XElement element)
        {
            return Attribute(element, "Path")
                ?? Attribute(element, "Name")
                ?? element.Name.LocalName;
        }

        private static int GetFolderOrder(XElement element)
        {
            switch (element.Name.LocalName)
            {
                case "File": return 0;
                case "Project": return 1;
                case "Properties": return 2;
                default: return int.MaxValue;
            }
        }

        private static string GetProjectRuleKey(XElement element)
        {
            return (Attribute(element, "Solution") ?? string.Empty)
                + "\0"
                + (Attribute(element, "Project") ?? string.Empty)
                + "\0"
                + (Attribute(element, "Name") ?? string.Empty);
        }

        private static int GetProjectRuleOrder(XElement element)
        {
            switch (element.Name.LocalName)
            {
                case "BuildDependency": return 0;
                case "BuildType": return 1;
                case "Platform": return 2;
                case "Build": return 3;
                case "Deploy": return 4;
                case "Properties": return 5;
                default: return int.MaxValue;
            }
        }

        private static string GetSolutionKey(XElement element)
        {
            return Attribute(element, "Name")
                ?? Attribute(element, "Path")
                ?? element.Name.LocalName;
        }

        private static int GetSolutionOrder(XElement element)
        {
            switch (element.Name.LocalName)
            {
                case "Configurations": return 0;
                case "Folder": return 1;
                case "Project": return 2;
                case "Properties": return 3;
                default: return int.MaxValue;
            }
        }

        private static bool IsLayoutWhitespace(XNode node)
        {
            return node is XText text && node.NodeType == XmlNodeType.Text
                && text.Value.All(character => character == ' ' || character == '\t' || character == '\r' || character == '\n');
        }

        private static void SortChildren(
            XContainer parent,
            Func<XElement, int> order,
            Func<XElement, string> key,
            bool sortEntries,
            bool formatLayout)
        {
            if (parent is XElement parentElement && parentElement.AncestorsAndSelf().Any(element =>
                (string)element.Attribute(XNamespace.Xml + "space") == "preserve"
                || element.Nodes().OfType<XText>().Any(text => !IsLayoutWhitespace(text))))
            {
                return;
            }

            parent.AddAnnotation(new LayoutContainer());
            if (!sortEntries)
            {
                return;
            }

            var groups = new List<ElementGroup>();
            var leadingNodes = new List<XNode>();

            foreach (var node in parent.Nodes().ToList())
            {
                if (node is XElement element)
                {
                    groups.Add(new ElementGroup(element, new List<XNode>(leadingNodes)));
                    leadingNodes.Clear();
                }
                else if (!formatLayout || !IsLayoutWhitespace(node))
                {
                    leadingNodes.Add(node);
                }
            }

            if (groups.Count == 0)
            {
                return;
            }

            var nodes = new List<XNode>();
            var sortableGroups = new List<ElementGroup>();
            foreach (var group in groups)
            {
                // SLNX elements are unqualified. An extension can reuse a known
                // local name without becoming part of a sortable SLNX run.
                if (group.Element.Name.Namespace == XNamespace.None
                    && order(group.Element) != int.MaxValue)
                {
                    sortableGroups.Add(group);
                    continue;
                }

                AddSortedGroups(sortableGroups, nodes, order, key);
                nodes.AddRange(group.LeadingNodes);
                nodes.Add(group.Element);
            }

            AddSortedGroups(sortableGroups, nodes, order, key);
            nodes.AddRange(leadingNodes);
            parent.ReplaceNodes(nodes);
        }

        private static void SortProject(XElement project, bool sortEntries, bool formatLayout)
        {
            SortChildren(project, GetProjectRuleOrder, GetProjectRuleKey, sortEntries, formatLayout);
            SortProperties(project, sortEntries, formatLayout);
        }

        private static void SortProperties(XContainer parent, bool sortEntries, bool formatLayout)
        {
            foreach (var properties in parent.Elements("Properties"))
            {
                SortChildren(
                    properties,
                    element => element.Name.LocalName == "Property" ? 0 : int.MaxValue,
                    element => Attribute(element, "Name") ?? string.Empty,
                    sortEntries,
                    formatLayout);
            }
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

        private sealed class LayoutContainer
        {
        }

        private sealed class LayoutWhitespace : XText
        {
            public LayoutWhitespace(string value) : base(value) { }

            public override void WriteTo(XmlWriter writer)
            {
                // Entitize protects extension data, but layout must contain real
                // CR/CRLF characters, including outside the document element.
                writer.WriteRaw(this.Value);
            }
        }

    }
}
