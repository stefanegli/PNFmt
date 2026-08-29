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
        public static string Format(string text)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreWhitespace = true,
                XmlResolver = null,
            };

            XDocument document;
            using (var stringReader = new StringReader(text))
            using (var xmlReader = XmlReader.Create(stringReader, readerSettings))
            {
                document = XDocument.Load(xmlReader);
            }

            var root = document.Root;
            if (root is null || root.Name != XName.Get("Solution"))
            {
                throw new InvalidDataException("The file does not contain an SLNX Solution root element.");
            }

            SortChildren(root, GetSolutionOrder, GetSolutionKey);

            foreach (var configurations in root.Elements("Configurations"))
            {
                SortChildren(configurations, GetConfigurationOrder, GetConfigurationKey);
                foreach (var projectType in configurations.Elements("ProjectType"))
                {
                    SortChildren(projectType, GetProjectRuleOrder, GetProjectRuleKey);
                }
            }

            foreach (var folder in root.Elements("Folder"))
            {
                SortChildren(folder, GetFolderOrder, GetFolderKey);
                SortProperties(folder);
                foreach (var project in folder.Elements("Project"))
                {
                    SortProject(project);
                }
            }

            foreach (var project in root.Elements("Project"))
            {
                SortProject(project);
            }

            SortProperties(root);

            var newLine = DetectNewLine(text);
            var writerSettings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                IndentChars = "  ",
                NewLineChars = newLine,
                NewLineHandling = NewLineHandling.Replace,
                OmitXmlDeclaration = document.Declaration is null,
            };

            using (var writer = new Utf8StringWriter())
            using (var xmlWriter = XmlWriter.Create(writer, writerSettings))
            {
                document.Save(xmlWriter);
                xmlWriter.Flush();
                var formatted = writer.ToString();
                return formatted.EndsWith(newLine, StringComparison.Ordinal)
                    ? formatted
                    : formatted + newLine;
            }
        }

        private static string DetectNewLine(string text)
        {
            if (text.IndexOf("\r\n", StringComparison.Ordinal) >= 0)
            {
                return "\r\n";
            }

            if (text.IndexOf('\r') >= 0)
            {
                return "\r";
            }

            return "\n";
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

        private static string GetConfigurationKey(XElement element)
        {
            return Attribute(element, "Name")
                ?? Attribute(element, "TypeId")
                ?? Attribute(element, "Extension")
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

        private static string GetFolderKey(XElement element)
        {
            return Attribute(element, "Path")
                ?? Attribute(element, "Name")
                ?? element.Name.LocalName;
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

        private static string GetProjectRuleKey(XElement element)
        {
            return (Attribute(element, "Solution") ?? string.Empty)
                + "\0"
                + (Attribute(element, "Project") ?? string.Empty)
                + "\0"
                + (Attribute(element, "Name") ?? string.Empty);
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

        private static string GetSolutionKey(XElement element)
        {
            return Attribute(element, "Name")
                ?? Attribute(element, "Path")
                ?? element.Name.LocalName;
        }

        private static void SortProject(XElement project)
        {
            SortChildren(project, GetProjectRuleOrder, GetProjectRuleKey);
            SortProperties(project);
        }

        private static void SortProperties(XContainer parent)
        {
            foreach (var properties in parent.Elements("Properties"))
            {
                SortChildren(
                    properties,
                    element => element.Name.LocalName == "Property" ? 0 : int.MaxValue,
                    element => Attribute(element, "Name") ?? string.Empty);
            }
        }

        private static void SortChildren(
            XContainer parent,
            Func<XElement, int> order,
            Func<XElement, string> key)
        {
            var groups = new List<ElementGroup>();
            var leadingNodes = new List<XNode>();

            foreach (var node in parent.Nodes().ToList())
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

            if (groups.Count == 0)
            {
                return;
            }

            var nodes = new List<XNode>();
            var sortableGroups = new List<ElementGroup>();
            foreach (var group in groups)
            {
                if (order(group.Element) != int.MaxValue)
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

        private sealed class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
        }
    }
}
