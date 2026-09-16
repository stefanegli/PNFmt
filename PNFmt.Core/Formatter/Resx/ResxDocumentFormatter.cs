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
            var hasSchemaRemoved = false;
            var hasCommentRemoved = false;
            var toSave = new List<XNode>();
            var toSort = new List<ResourceEntry>();
            var pendingComments = new List<XNode>();
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
            if (!IsResx(root) || HasUnnamedResourceEntry(root))
            {
                var reason = !IsResx(root) ? "The document is not a RESX resource file."
                    : "Resource data and metadata entries must have a name attribute.";
                var lineInfo = (IXmlLineInfo)root;
                return DocumentFormatResult.Skipped(
                    new FormatterDiagnostic("RESX001", "Formatting skipped: " + reason,
                        lineInfo?.HasLineInfo() == true ? (int?)lineInfo.LineNumber : null));
            }

            if (formatLayout)
            {
                RemoveLayoutWhitespace(document);
            }

            foreach (var node in root.Nodes())
            {
                if (this.Settings.RemoveXsdSchema)
                {
                    if (!hasSchemaRemoved && node is XElement e && IsXsdSchema(e))
                    {
                        toSave.AddRange(pendingComments);
                        pendingComments.Clear();
                        toSave.Add(XElement.Parse(ResxSchemaDefaults.FakeSchema));
                        hasSchemaRemoved = true;
                        continue;
                    }
                }

                if (this.Settings.RemoveDocumentationComment)
                {
                    if (!hasCommentRemoved && node is XComment comment && IsDocumentationComment(comment))
                    {
                        hasCommentRemoved = true;
                        continue;
                    }
                }

                if (node is XElement element && IsResourceEntry(element))
                {
                    toSort.Add(new ResourceEntry(element, pendingComments.ToArray()));
                    pendingComments.Clear();
                }
                else if (node is XComment resourceComment && !IsDocumentationComment(resourceComment))
                {
                    pendingComments.Add(node);
                }
                else
                {
                    toSave.AddRange(pendingComments);
                    pendingComments.Clear();
                    toSave.Add(node);
                }
            }

            var sorted = this.Settings.SortEntries
                ? toSort.OrderBy(entry => entry.Element.Name.ToString(), this.Settings.Comparer)
                    .ThenBy(entry => entry.Element.Attribute("name").Value, this.Settings.Comparer)
                    .ToList()
                : toSort;

            var hasCommentAdded = false;
            if (this.Settings.InsertDocumentationComment && !this.Settings.RemoveDocumentationComment && !HasDocumentationComment(document))
            {
                // XML readers normalize comment line endings to LF. Match that
                // representation immediately so a second rewrite stays identical.
                toSave.Insert(0, new XComment(ResxSchemaDefaults.OriginalCommentContent
                    .Replace("\r\n", "\n").Replace('\r', '\n')));
                hasCommentAdded = true;
            }

            var hasSchemaAdded = false;
            if (this.Settings.InsertXsdSchema && !this.Settings.RemoveXsdSchema && !HasSchemaNode(document))
            {
                toSave.Insert(Math.Min(1, toSave.Count), XElement.Parse(ResxSchemaDefaults.OriginalSchema));
                hasSchemaAdded = true;
            }

            var requiresSorting = this.Settings.SortEntries && !toSort.SequenceEqual(sorted);
            var hasContentChanges = hasSchemaRemoved || hasCommentRemoved || hasCommentAdded || hasSchemaAdded || requiresSorting;
            if (hasContentChanges)
            {
                foreach (var entry in sorted)
                {
                    toSave.AddRange(entry.LeadingComments);
                    toSave.Add(entry.Element);
                }

                // Comments without a following resource entry are file footers.
                toSave.AddRange(pendingComments);
                document.Root.ReplaceNodes(toSave);
            }

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

        private static bool HasUnnamedResourceEntry(XElement root)
        {
            return root.Elements().Any(element =>
                IsResourceEntry(element) && element.Attribute("name") is null);
        }

        private static bool IsDocumentationComment(XComment comment)
        {
            return RemoveWhiteSpace(comment.ToString()) == RemoveWhiteSpace(ResxSchemaDefaults.OriginalComment);
        }

        private static bool IsXsdSchema(XElement element)
        {
            return element.Name == XName.Get("schema", "http://www.w3.org/2001/XMLSchema");
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

        private static string RemoveWhiteSpace(string text)
        {
            return string.Join("", text.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
