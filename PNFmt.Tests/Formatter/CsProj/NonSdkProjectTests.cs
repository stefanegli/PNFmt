using System.IO;
using System.Linq;
using System.Xml.Linq;

using Xunit;

namespace PNFmt.Tests.Formatter.CsProj
{
    public sealed class NonSdkProjectTests
    {
        [Theory]
        [InlineData("", false)]
        [InlineData("", true)]
        [InlineData("http://schemas.microsoft.com/developer/msbuild/2003", false)]
        [InlineData("http://schemas.microsoft.com/developer/msbuild/2003", true)]
        public void Formatting_preserves_legacy_structure_and_honors_sorting(string xmlNamespace, bool sort)
        {
            var input = "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                + "<Project xmlns=\"" + xmlNamespace + "\" ToolsVersion=\"15.0\" DefaultTargets=\"Build\">"
                + "<Import Project=\"Common.props\" />"
                + "<PropertyGroup Condition=\"'$(Configuration)' == 'Debug'\"><Zebra>z</Zebra><Alpha>a</Alpha></PropertyGroup>"
                + "<ItemGroup><Reference Include=\"Zebra\"/><Reference Include=\"Alpha\"/></ItemGroup>"
                + "<ImportGroup><Import Project=\"Common.targets\"/></ImportGroup>"
                + "<Choose><When Condition=\"'$(Configuration)' == 'Debug'\"><PropertyGroup><Zebra>z</Zebra><Alpha>a</Alpha></PropertyGroup></When></Choose>"
                + "<ItemDefinitionGroup><Compile><Zebra>z</Zebra><Alpha>a</Alpha></Compile></ItemDefinitionGroup>"
                + "<UsingTask TaskName=\"Inline\" TaskFactory=\"RoslynCodeTaskFactory\" AssemblyFile=\"Tasks.dll\">"
                + "<Task><Code Type=\"Fragment\" Language=\"cs\"><![CDATA[\nvar text = \"<tag>\";\nLog.LogMessage(text);\n]]></Code></Task></UsingTask>"
                + "<Target Name=\"Build\"><PropertyGroup><Zebra>z</Zebra><Alpha>$(Zebra)</Alpha></PropertyGroup>"
                + "<ItemGroup><Compile Include=\"Z.cs\"/><Compile Include=\"A.cs\"/></ItemGroup>"
                + "<Message Text=\"first\"/><Message Text=\"second\"/></Target>"
                + "<ProjectExtensions><VisualStudio><Zebra>z</Zebra><Alpha>a</Alpha></VisualStudio></ProjectExtensions>"
                + "</Project>";
            using (var directory = new TestDirectory())
            {
                directory.Write(".editorconfig", "root = true\n[*]\npnfmt_enabled = true\npnfmt_formatter = csproj\n"
                    + "end_of_line = lf\nindent_style = space\nindent_size = 2\npnfmt_sort_entries = " + sort + "\n");
                var path = directory.Write("Legacy.csproj", input);
                var formatter = new CsProjFormatter();
                var preview = formatter.Format(new FileFormatRequest(path, false, false, NullFormatterLog.Instance));
                Assert.Equal(FileFormatStatus.Updated, preview.Status);
                Assert.Equal(input, File.ReadAllText(path));

                Assert.Equal(FileFormatStatus.Updated,
                    formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
                var formatted = File.ReadAllText(path);
                Assert.Contains("\n  <PropertyGroup", formatted);
                var before = XDocument.Parse(input);
                var after = XDocument.Parse(formatted);
                XNamespace ns = xmlNamespace;
                Assert.Equal(before.Declaration.ToString(), after.Declaration.ToString());
                Assert.Equal(before.Root.Name, after.Root.Name);
                Assert.Equal(before.Root.Attributes().Select(attribute => attribute.ToString()),
                    after.Root.Attributes().Select(attribute => attribute.ToString()));
                Assert.Equal(before.Root.Elements().Select(element => element.Name), after.Root.Elements().Select(element => element.Name));
                Assert.Equal(sort ? new[] { "Alpha", "Zebra" } : new[] { "Zebra", "Alpha" },
                    after.Root.Element(ns + "PropertyGroup").Elements().Select(element => element.Name.LocalName));
                Assert.Equal(sort ? new[] { "Alpha", "Zebra" } : new[] { "Zebra", "Alpha" },
                    after.Root.Element(ns + "ItemGroup").Elements().Select(element => (string)element.Attribute("Include")));
                foreach (var element in before.Root.Elements().Where(element => element.Name.LocalName != "PropertyGroup" && element.Name.LocalName != "ItemGroup"))
                {
                    Assert.True(XNode.DeepEquals(element, after.Root.Element(element.Name)), element.Name.LocalName);
                }

                Assert.Equal(FileFormatStatus.Unchanged,
                    formatter.Format(new FileFormatRequest(path, true, false, NullFormatterLog.Instance)).Status);
                Assert.Equal(formatted, File.ReadAllText(path));
            }
        }

        [Theory]
        [InlineData("<Root><Project/></Root>")]
        [InlineData("<Root Sdk=\"Microsoft.NET.Sdk\"/>")]
        public void Non_project_documents_are_still_skipped(string input)
        {
            using (var directory = new TestDirectory())
            {
                directory.Write(".editorconfig", "root = true\n[*]\npnfmt_enabled = true\npnfmt_formatter = csproj\n");
                var path = directory.Write("Invalid.csproj", input);
                var result = new CsProjFormatter().Format(new FileFormatRequest(path, true, true, NullFormatterLog.Instance));
                Assert.Equal(FileFormatStatus.Skipped, result.Status);
                Assert.Empty(result.Diagnostics);
                Assert.Equal(input, File.ReadAllText(path));
            }
        }
    }
}
