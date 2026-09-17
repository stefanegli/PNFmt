namespace PNFmt.Tests.Formatter.Resx
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Linq;

    using PNFmt;
    using PNFmt.Tests.Formatter.Resx.Fake;
    using PNFmt.Tests.Formatter.Resx.TestFoundation;

    using Xunit;

    public class AdversarialFormattingTests
    {
        private const string Header = "<resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>";

        [Fact]
        public void Already_minimized_schema_is_idempotent()
        {
            using (var file = TemporaryFile.Create(Resx("<schema /><data name=\"a\"><value>1</value></data>")))
            {
                var original = File.ReadAllText(file.Path);
                var formatter = new ResxDocumentFormatter(SortAndRemoveDefaults());

                Assert.Equal(FileFormatStatus.Unchanged, formatter.Run(new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance)).Status);
                Assert.Equal(original, File.ReadAllText(file.Path));
            }
        }

        [Fact]
        public void Alternate_xsd_schema_is_recognized_and_minimized()
        {
            const string Schema = "<xs:schema xmlns:xs=\"http://www.w3.org/2001/XMLSchema\"><xs:element name=\"custom\" /></xs:schema>";
            var document = XDocument.Parse(Resx(Schema + "<data name=\"a\"><value>1</value></data>"));
            Assert.True(ResxDocumentFormatter.HasSchemaNode(document));

            using (var file = TemporaryFile.Create(document.ToString()))
            {
                var formatter = new ResxDocumentFormatter(SortAndRemoveDefaults());
                Assert.Equal(FileFormatStatus.Updated, formatter.Run(new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance)).Status);
                var formatted = XDocument.Load(file.Path);
                Assert.NotNull(formatted.Root.Element("schema"));
                Assert.False(ResxDocumentFormatter.HasSchemaNode(formatted));
            }
        }

        [Fact]
        public void Constructor_rejects_missing_settings_at_the_boundary()
        {
            Assert.Throws<ArgumentNullException>(() => new ResxDocumentFormatter(null));
        }

        [Fact]
        public void Custom_schema_element_is_not_mistaken_for_the_resx_xsd()
        {
            using (var file = TemporaryFile.Create(Resx("<schema owner=\"application\" /><data name=\"a\"><value>1</value></data>")))
            {
                var formatter = new ResxDocumentFormatter(SortAndRemoveDefaults());

                Assert.Equal(FileFormatStatus.Unchanged, formatter.Run(new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance)).Status);
                Assert.Equal("application", (string)XDocument.Load(file.Path).Root.Element("schema").Attribute("owner"));
            }
        }

        [Fact]
        public void Detection_helpers_reject_a_missing_document()
        {
            Assert.Throws<ArgumentNullException>(() => ResxDocumentFormatter.HasDocumentationComment(null));
            Assert.Throws<ArgumentNullException>(() => ResxDocumentFormatter.HasSchemaNode(null));
        }

        [Fact]
        public void Documentation_is_found_and_removed_even_after_an_unrelated_comment()
        {
            var standard = XDocument.Load(Path.Combine(
                AppContext.BaseDirectory,
                "Formatter",
                "Resx",
                "_files",
                "Sort.resx"));
            var documentation = standard.Root.Nodes().OfType<XComment>().First();
            var document = XDocument.Parse(Resx("<!-- keep me -->" + documentation + "<data name=\"a\"><value>1</value></data>"));
            Assert.True(ResxDocumentFormatter.HasDocumentationComment(document));

            using (var file = TemporaryFile.Create(document.ToString()))
            {
                new ResxDocumentFormatter(SortAndRemoveDefaults()).Run(new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance));
                var formatted = XDocument.Load(file.Path);

                Assert.False(ResxDocumentFormatter.HasDocumentationComment(formatted));
                Assert.Single(formatted.Root.Nodes().OfType<XComment>());
                Assert.Equal("keep me", formatted.Root.Nodes().OfType<XComment>().Single().Value.Trim());
            }
        }

        [Fact]
        public void Duplicate_names_remain_stable_and_element_kind_is_the_primary_key()
        {
            var contents = Resx(
                "<metadata name=\"z\" marker=\"metadata\"><value>0</value></metadata>" +
                "<data name=\"same\" marker=\"first\"><value>1</value></data>" +
                "<data name=\"a\"><value>2</value></data>" +
                "<data name=\"same\" marker=\"second\"><value>3</value></data>");
            using (var file = TemporaryFile.Create(contents))
            {
                new ResxDocumentFormatter(SortAndRemoveDefaults()).Run(new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance));
                var entries = XDocument.Load(file.Path).Root.Elements()
                    .Where(element => element.Name == "data" || element.Name == "metadata")
                    .ToList();

                Assert.Equal(new[] { "data", "data", "data", "metadata" }, entries.Select(element => element.Name.LocalName));
                Assert.Equal(new[] { "a", "same", "same", "z" }, entries.Select(element => (string)element.Attribute("name")));
                Assert.Equal(new[] { "first", "second" }, entries.Where(element => (string)element.Attribute("name") == "same").Select(element => (string)element.Attribute("marker")));
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Entry_comments_and_file_footers_keep_their_association_during_rewrites(bool sort)
        {
            var input = "<root><!--" + ResxSchemaDefaults.OriginalCommentContent + "-->"
                + "<!--file header-->" + Header
                + "<!--B first--><!--B second--><data name='b'><value>2</value></data>"
                + "<!--A--><data name='a'><value>1</value></data><!--footer--></root>";
            using (var file = TemporaryFile.Create(input))
            {
                var settings = SortAndRemoveDefaults();
                settings.SortEntries = sort;
                var formatter = new ResxDocumentFormatter(settings);
                formatter.Run(new FileFormatRequest(file.Path, false, false, NullFormatterLog.Instance));
                Assert.Equal(input, File.ReadAllText(file.Path));
                formatter.Run(new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance));
                var root = XDocument.Load(file.Path).Root;
                Assert.Equal("file header", Assert.IsType<XComment>(root.FirstNode).Value);
                Assert.Equal("footer", Assert.IsType<XComment>(root.LastNode).Value);
                foreach (var entry in root.Elements("data"))
                {
                    var expected = (string)entry.Attribute("name") == "a" ? "A" : "B second";
                    Assert.Equal(expected, Assert.IsType<XComment>(entry.PreviousNode).Value);
                }

                var b = root.Elements("data").Single(entry => (string)entry.Attribute("name") == "b");
                Assert.Equal("B first", Assert.IsType<XComment>(b.PreviousNode.PreviousNode).Value);
                Assert.Equal(sort ? new[] { "a", "b" } : new[] { "b", "a" }, EntryNames(new XDocument(root)));
                var formatted = File.ReadAllBytes(file.Path);
                Assert.Equal(FileFormatStatus.Unchanged, formatter.Run(new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance)).Status);
                Assert.Equal(formatted, File.ReadAllBytes(file.Path));
            }
        }

        [Fact]
        public void Large_reverse_sorted_file_is_handled_and_second_run_is_idempotent()
        {
            const int EntryCount = 4096;
            var entries = new StringBuilder();
            for (var index = EntryCount - 1; index >= 0; index--)
            {
                entries.AppendFormat(CultureInfo.InvariantCulture, "<data name=\"key-{0:D4}\"><value>{0}</value></data>", index);
            }

            using (var file = TemporaryFile.Create(Resx(entries.ToString())))
            {
                var formatter = new ResxDocumentFormatter(SortAndRemoveDefaults());

                Assert.Equal(FileFormatStatus.Updated, formatter.Run(new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance)).Status);
                var names = EntryNames(XDocument.Load(file.Path));
                Assert.Equal(EntryCount, names.Count);
                Assert.Equal("key-0000", names[0]);
                Assert.Equal("key-4095", names[names.Count - 1]);

                Assert.Equal(FileFormatStatus.Unchanged, formatter.Run(new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance)).Status);
            }
        }

        [Fact]
        public void Namespaced_extension_elements_are_preserved_outside_the_sort_set()
        {
            var contents = Resx(
                "<ext:data xmlns:ext=\"urn:extension\" name=\"extension\"><ext:value>keep</ext:value></ext:data>" +
                "<data name=\"b\"><value>2</value></data><data name=\"a\"><value>1</value></data>");
            using (var file = TemporaryFile.Create(contents))
            {
                new ResxDocumentFormatter(SortAndRemoveDefaults()).Run(new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance));
                var document = XDocument.Load(file.Path);

                Assert.Equal("keep", document.Root.Element(XName.Get("data", "urn:extension")).Value);
                Assert.Equal(new[] { "a", "b" }, EntryNames(document));
            }
        }

        [Theory]
        [InlineData("<root><resheader><value>text/microsoft-resx</value></resheader></root>")]
        [InlineData("<root><resheader name=\"resmimetype\" /></root>")]
        [InlineData("<root><data name=\"a\"><value>1</value></data></root>")]
        public void Non_resx_xml_is_skipped_without_mutation(string contents)
        {
            using (var file = TemporaryFile.Create(contents))
            {
                var formatter = new ResxDocumentFormatter(SortAndRemoveDefaults());

                var result = formatter.Run(new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance));
                Assert.Equal(FileFormatStatus.Skipped, result.Status);
                Assert.Equal("RESX001", Assert.Single(result.Diagnostics).Code);
                Assert.Equal(contents, File.ReadAllText(file.Path));
            }
        }

        [Fact]
        public void One_document_formatter_can_process_independent_files_concurrently()
        {
            var failures = new ConcurrentQueue<Exception>();
            var formatter = new ResxDocumentFormatter(SortAndRemoveDefaults());
            Parallel.For(0, 32, iteration =>
            {
                try
                {
                    using (var file = TemporaryFile.Create(Resx("<data name=\"b\"><value>2</value></data><data name=\"a\"><value>1</value></data>")))
                    {
                        Assert.Equal(FileFormatStatus.Updated, formatter.Run(new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance)).Status);
                        Assert.Equal(new[] { "a", "b" }, EntryNames(XDocument.Load(file.Path)));
                    }
                }
                catch (Exception ex)
                {
                    failures.Enqueue(ex);
                }
            });

            Assert.Empty(failures);
        }

        [Fact]
        public void Removing_documentation_preserves_unrelated_comments()
        {
            using (var file = TemporaryFile.Create(Resx("<!-- keep me --><data name=\"b\"><value>2</value></data><data name=\"a\"><value>1</value></data>")))
            {
                var formatter = new ResxDocumentFormatter(SortAndRemoveDefaults());

                formatter.Run(new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance));

                var document = XDocument.Load(file.Path);
                Assert.Contains(document.Root.Nodes().OfType<XComment>(), comment => comment.Value.Trim() == "keep me");
                Assert.Equal(new[] { "a", "b" }, EntryNames(document));
            }
        }

        [Fact]
        public void Returned_results_are_independent_of_later_operations()
        {
            using (var valid = TemporaryFile.Create(Resx("<data name=\"b\"><value>2</value></data><data name=\"a\"><value>1</value></data>")))
            using (var malformed = TemporaryFile.Create("<root>"))
            using (var invalid = TemporaryFile.Create("<root />"))
            {
                var formatter = new ResxDocumentFormatter(SortAndRemoveDefaults());
                var updated = formatter.Run(new FileFormatRequest(valid.Path, true, false, NullFormatterLog.Instance));
                var skipped = formatter.Run(new FileFormatRequest(invalid.Path, true, false, NullFormatterLog.Instance));

                Assert.Throws<XmlException>(() => formatter.Run(new FileFormatRequest(malformed.Path, true, false, NullFormatterLog.Instance)));
                var unchanged = formatter.Run(new FileFormatRequest(valid.Path, true, false, NullFormatterLog.Instance));

                Assert.Equal(FileFormatStatus.Updated, updated.Status);
                Assert.Empty(updated.Diagnostics);
                Assert.Equal(FileFormatStatus.Skipped, skipped.Status);
                Assert.Equal("RESX001", Assert.Single(skipped.Diagnostics).Code);
                Assert.Equal(FileFormatStatus.Unchanged, unchanged.Status);
                Assert.Empty(unchanged.Diagnostics);
            }
        }

        [Fact]
        public void Unnamed_resource_entry_invalidates_the_file_without_partial_rewrites()
        {
            var contents = Resx("<data><value>unnamed</value></data><data name=\"b\"><value>2</value></data><data name=\"a\"><value>1</value></data>");
            using (var file = TemporaryFile.Create(contents))
            {
                var formatter = new ResxDocumentFormatter(SortAndRemoveDefaults());

                var result = formatter.Run(new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance));
                Assert.Equal(FileFormatStatus.Skipped, result.Status);
                Assert.Equal("RESX001", Assert.Single(result.Diagnostics).Code);
                Assert.Equal(contents, File.ReadAllText(file.Path));
            }
        }

        [Theory]
        [InlineData("<root>")]
        [InlineData("<!-- no document element -->")]
        [InlineData("<!DOCTYPE root [<!ENTITY repeated 'payload'>]><root><resheader name=\"resmimetype\"><value>&repeated;</value></resheader></root>")]
        public void Unsafe_or_malformed_xml_is_rejected_without_corrupting_the_source(string contents)
        {
            using (var file = TemporaryFile.Create(contents))
            {
                var formatter = new ResxDocumentFormatter(SortAndRemoveDefaults());

                Assert.Throws<XmlException>(() => formatter.Run(new FileFormatRequest(file.Path, true, false, NullFormatterLog.Instance)));
                Assert.Equal(contents, File.ReadAllText(file.Path));
            }
        }

        private static List<string> EntryNames(XDocument document)
        {
            return document.Root.Elements("data").Select(element => (string)element.Attribute("name")).ToList();
        }

        private static string Resx(string body)
        {
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?><root>" + Header + body + "</root>";
        }

        private static FakeSettings SortAndRemoveDefaults()
        {
            return new FakeSettings
            {
                Comparer = StringComparer.Ordinal,
                SortEntries = true,
                RemoveDocumentationComment = true,
                RemoveXsdSchema = true
            };
        }
    }
}
