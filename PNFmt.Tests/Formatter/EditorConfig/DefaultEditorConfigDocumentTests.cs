// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System.IO;
using PNFmt.Tests.Snapshots;
using Xunit;

namespace PNFmt.Tests.Formatter.EditorConfig
{
    public sealed class DefaultEditorConfigDocumentTests
    {
        [Fact]
        public void Default_configuration_matches_snapshot_and_is_idempotent()
        {
            var actual = DefaultEditorConfigDocument.Update(string.Empty);

            Assert.Equal(actual, DefaultEditorConfigDocument.Update(actual));
            GitSnapshot.Match(
                actual,
                typeof(DefaultEditorConfigDocumentTests),
                "Default.editorconfig");
        }

        [Fact]
        public void Existing_rules_around_the_managed_block_are_preserved()
        {
            const string Existing =
                "root = false\n\n[*]\ncharset = utf-8\n\n"
                + DefaultEditorConfigDocument.StartMarker
                + "\nold = value\n"
                + DefaultEditorConfigDocument.EndMarker
                + "\n\n[*.md]\ntrim_trailing_whitespace = true\n";

            var updated = DefaultEditorConfigDocument.Update(Existing);

            Assert.StartsWith("root = false\n\n[*]\ncharset = utf-8", updated);
            Assert.DoesNotContain("old = value", updated);
            Assert.EndsWith("[*.md]\ntrim_trailing_whitespace = true\n", updated);
        }

        [Theory]
        [InlineData("# <pnfmt-defaults>\n")]
        [InlineData("# </pnfmt-defaults>\n# <pnfmt-defaults>\n")]
        [InlineData("# <pnfmt-defaults>\n# <pnfmt-defaults>\n# </pnfmt-defaults>\n")]
        public void Invalid_managed_markers_are_rejected(string contents)
        {
            Assert.Throws<InvalidDataException>(
                () => DefaultEditorConfigDocument.Update(contents));
        }
    }
}
