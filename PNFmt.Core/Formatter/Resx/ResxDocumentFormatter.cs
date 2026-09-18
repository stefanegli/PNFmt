namespace PNFmt
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using System.Xml.Linq;

    internal sealed class ResxDocumentFormatter
    {
        public ResxDocumentFormatter(IResxFormatSettings settings)
        {
            this.Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        private IResxFormatSettings Settings { get; }

        public static bool HasDocumentationComment(XDocument document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var schema = RemoveWhiteSpace(ResxSchemaDefaults.OriginalComment);
            return document.Root?.Nodes()
                .OfType<XComment>()
                .Any(comment => RemoveWhiteSpace(comment.ToString()) == schema) == true;

            string RemoveWhiteSpace(string text) => string.Join("", text.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        }

        public static bool HasSchemaNode(XDocument document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return document.Root?.Elements().Any(IsXsdSchema) == true;
        }

        public FileFormatResult Run(FileFormatRequest request, bool formatLayout = true, bool hasExplicitLayout = false)
        {
            return TextFileFormatPipeline.Format(request, true,
                (text, encoding) => this.FormatResx(text, encoding, formatLayout, hasExplicitLayout), xml: true);
        }

        private DocumentFormatResult FormatResx(string originalText, Encoding encoding, bool formatLayout, bool hasExplicitLayout)
        {
            XDocument document;
            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreWhitespace = false,
                XmlResolver = null
            };

            using (var textReader = new StringReader(originalText))
            using (var reader = XmlReader.Create(textReader, readerSettings))
            {
                document = XDocument.Load(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            }

            var root = document.Root;
            var diagnostic = GetRootDiagnostic(root);
            if (diagnostic is not null)
            {
                return DocumentFormatResult.Skipped(diagnostic);
            }

            if (formatLayout)
            {
                RemoveLayoutWhitespace(document);
            }

            var hasContentChanges = new ResourceContent(this.Settings).Rewrite(document);

            if (hasContentChanges || (formatLayout && (hasExplicitLayout || this.Settings.Layout?.HasOverrides == true)) || encoding is not null)
            {
                var layout = this.Settings.Layout ?? new ResxLayoutSettings(new Dictionary<string, string>());
                if (!formatLayout && !hasContentChanges && encoding is not null)
                {
                    return DocumentFormatResult.FromText(originalText);
                }

                return DocumentFormatResult.FromBytes(layout.Serialize(document, encoding, formatLayout));
            }

            // Legacy resource settings only rewrite when content actually changes.
            return DocumentFormatResult.FromText(originalText);
        }

        private static FormatterDiagnostic GetRootDiagnostic(XElement root)
        {
            if (!IsResx(root) || HasUnnamedResourceEntry(root))
            {
                var reason = !IsResx(root) ? "The document is not a RESX resource file."
                    : "Resource data and metadata entries must have a name attribute.";
                var lineInfo = (IXmlLineInfo)root;
                return new FormatterDiagnostic("RESX001", "Formatting skipped: " + reason,
                    lineInfo?.HasLineInfo() == true ? (int?)lineInfo.LineNumber : null);
            }

            return null;
        }

        private static bool HasUnnamedResourceEntry(XElement root)
        {
            return root.Elements().Any(element =>
                IsResourceEntry(element) && element.Attribute("name") is null);
        }

        private static bool IsDocumentationComment(XComment comment)
        {
            return RemoveWhiteSpace(comment.ToString()) == RemoveWhiteSpace(ResxSchemaDefaults.OriginalComment);
        }

        private static bool IsResourceEntry(XElement element)
        {
            return element.Name.Namespace == XNamespace.None
                && (element.Name.LocalName == "data" || element.Name.LocalName == "metadata");
        }

        private static bool IsResx(XElement root)
        {
            if (root?.Name != XName.Get("root"))
            {
                return false;
            }

            return root.Elements("resheader").Any(element =>
                (string)element.Attribute("name") == "resmimetype"
                && (string)element.Element("value") == "text/microsoft-resx");
        }

        private static bool IsXsdSchema(XElement element)
        {
            return element.Name == XName.Get("schema", "http://www.w3.org/2001/XMLSchema");
        }

        private static void RemoveLayoutWhitespace(XDocument document)
        {
            // Whitespace in resource values and comments is application data even
            // without xml:space. Remove only surrounding XML layout, retaining the
            // reader's existing xml:space behavior elsewhere in the document.
            var layout = document.DescendantNodes().OfType<XText>()
                .Where(text => text.NodeType == XmlNodeType.Text
                    && text.Value.All(character => character == ' ' || character == '\t' || character == '\r' || character == '\n')
                    && !text.Ancestors().Any(element => element.Parent is not null
                        && IsResourceEntry(element.Parent))
                    && (string)text.Ancestors().Attributes(XNamespace.Xml + "space").FirstOrDefault() != "preserve")
                .ToList();
            foreach (var text in layout)
            {
                text.Remove();
            }
        }

        private static string RemoveWhiteSpace(string text)
        {
            return string.Join("", text.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        }

        // Owns the association between resource entries and their leading comments,
        // and only replaces the document nodes when a content change is needed.
        private sealed class ResourceContent
        {
            private readonly IResxFormatSettings settings;
            private readonly List<XNode> preserved = new List<XNode>();
            private readonly List<ResourceEntry> entries = new List<ResourceEntry>();
            private readonly List<XNode> pendingComments = new List<XNode>();
            private bool schemaRemoved;
            private bool commentRemoved;

            public ResourceContent(IResxFormatSettings settings)
            {
                this.settings = settings;
            }

            public bool Rewrite(XDocument document)
            {
                foreach (var node in document.Root.Nodes())
                {
                    this.AddNode(node);
                }

                var sorted = this.settings.SortEntries
                    ? this.entries.OrderBy(entry => entry.Element.Name.ToString(), this.settings.Comparer)
                        .ThenBy(entry => entry.Element.Attribute("name").Value, this.settings.Comparer).ToList()
                    : this.entries;
                var metadataAdded = this.InsertMissingMetadata(document);
                var requiresSorting = this.settings.SortEntries && !this.entries.SequenceEqual(sorted);
                if (!this.schemaRemoved && !this.commentRemoved && !metadataAdded && !requiresSorting)
                {
                    return false;
                }

                foreach (var entry in sorted)
                {
                    this.preserved.AddRange(entry.LeadingComments);
                    this.preserved.Add(entry.Element);
                }

                // Comments without a following resource entry are file footers.
                this.preserved.AddRange(this.pendingComments);
                document.Root.ReplaceNodes(this.preserved);
                return true;
            }

            private void AddNode(XNode node)
            {
                if (this.TryRemoveMetadata(node))
                {
                    return;
                }

                if (node is XElement element && IsResourceEntry(element))
                {
                    this.entries.Add(new ResourceEntry(element, this.pendingComments.ToArray()));
                    this.pendingComments.Clear();
                }
                else if (node is XComment comment && !IsDocumentationComment(comment))
                {
                    this.pendingComments.Add(node);
                }
                else
                {
                    this.PreserveNode(node);
                }
            }

            private bool InsertMissingMetadata(XDocument document)
            {
                var changed = false;
                if (this.settings.InsertDocumentationComment && !this.settings.RemoveDocumentationComment && !HasDocumentationComment(document))
                {
                    // Match XML readers' normalized comment line endings so a second
                    // rewrite produces the same bytes.
                    this.preserved.Insert(0, new XComment(ResxSchemaDefaults.OriginalCommentContent
                        .Replace("\r\n", "\n").Replace('\r', '\n')));
                    changed = true;
                }

                if (this.settings.InsertXsdSchema && !this.settings.RemoveXsdSchema && !HasSchemaNode(document))
                {
                    this.preserved.Insert(Math.Min(1, this.preserved.Count), XElement.Parse(ResxSchemaDefaults.OriginalSchema));
                    changed = true;
                }

                return changed;
            }

            private void PreserveNode(XNode node)
            {
                this.preserved.AddRange(this.pendingComments);
                this.pendingComments.Clear();
                this.preserved.Add(node);
            }

            private bool TryRemoveMetadata(XNode node)
            {
                if (this.settings.RemoveXsdSchema && !this.schemaRemoved && node is XElement element && IsXsdSchema(element))
                {
                    this.PreserveNode(XElement.Parse(ResxSchemaDefaults.FakeSchema));
                    this.schemaRemoved = true;
                    return true;
                }

                if (this.settings.RemoveDocumentationComment && !this.commentRemoved && node is XComment comment && IsDocumentationComment(comment))
                {
                    this.commentRemoved = true;
                    return true;
                }

                return false;
            }
        }

        private sealed class ResourceEntry
        {
            public ResourceEntry(XElement element, IReadOnlyList<XNode> leadingComments)
            {
                this.Element = element;
                this.LeadingComments = leadingComments;
            }

            public XElement Element { get; }
            public IReadOnlyList<XNode> LeadingComments { get; }
        }
    }
}
