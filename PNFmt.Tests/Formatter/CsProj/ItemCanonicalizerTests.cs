using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Xunit;

namespace PNFmt.Tests.Formatter.CsProj
{
    public sealed class ItemCanonicalizerTests
    {
        [Fact]
        public void Large_metadata_blocks_sort_without_losing_entries_or_comments()
        {
            const int Count = 4000;
            var item = new XElement("None", new XAttribute("Include", "file"));
            foreach (var index in Enumerable.Range(0, Count).Reverse())
            {
                item.Add(new XComment("entry " + index));
                item.Add(new XElement("Meta" + index.ToString("D5"), index));
            }

            var document = new XDocument(new XElement("Project", new XElement("ItemGroup", item)));
            ItemCanonicalizer.Canonicalize(document, new HashSet<string> { "None" });

            Assert.Equal(Enumerable.Range(0, Count).Select(index => "Meta" + index.ToString("D5")),
                item.Elements().Select(element => element.Name.LocalName));
            foreach (var element in item.Elements())
            {
                Assert.Equal("entry " + element.Value, Assert.IsType<XComment>(element.PreviousNode).Value);
            }

            var first = document.ToString();
            ItemCanonicalizer.Canonicalize(document, new HashSet<string> { "None" });
            Assert.Equal(first, document.ToString());
        }

        [Fact]
        public void Ready_queue_preserves_duplicate_metadata_and_reference_order()
        {
            var document = XDocument.Parse("<Project><ItemGroup><None Include='file'>"
                + "<Zebra>first</Zebra><Alpha>%(Zebra)</Alpha><zebra>second</zebra><Beta>independent</Beta>"
                + "</None></ItemGroup></Project>");

            ItemCanonicalizer.Canonicalize(document, new HashSet<string> { "None" });

            Assert.Equal(new[] { "Beta", "Zebra", "Alpha", "zebra" },
                document.Descendants("None").Single().Elements().Select(element => element.Name.LocalName));
            Assert.Equal(new[] { "independent", "first", "%(Zebra)", "second" },
                document.Descendants("None").Single().Elements().Select(element => element.Value));
        }
    }
}
