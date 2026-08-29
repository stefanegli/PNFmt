// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using Xunit;

namespace PNFmt.Tests
{
    public sealed class ProviderRegistryTests
    {
        [Fact]
        public void One_provider_can_handle_multiple_extensions()
        {
            var provider = new TestProvider("resource", "markup", ".xml");
            var registry = new FormatterProviderRegistry(new[] { provider });

            Assert.True(registry.TryGetProvider("settings.MARKUP", out var markupProvider));
            Assert.True(registry.TryGetProvider("settings.xml", out var xmlProvider));
            Assert.Same(provider, markupProvider);
            Assert.Same(provider, xmlProvider);
        }

        [Fact]
        public void Duplicate_extensions_are_rejected()
        {
            var first = new TestProvider("first", ".xml");
            var second = new TestProvider("second", "xml");

            Assert.Throws<InvalidOperationException>(
                () => new FormatterProviderRegistry(new[] { first, second }));
        }

        [Fact]
        public void Default_catalog_registers_the_current_file_types()
        {
            var registry = FormatterProviderCatalog.CreateDefault();

            Assert.True(registry.TryGetProvider("Project.CSPROJ", out var csprojProvider));
            Assert.True(registry.TryGetProvider("Strings.RESX", out var resxProvider));
            Assert.Equal("csproj", csprojProvider.Name);
            Assert.Equal("resx", resxProvider.Name);
        }

        private sealed class TestProvider : IFileFormatterProvider
        {
            public TestProvider(string name, params string[] extensions)
            {
                this.Name = name;
                this.FileExtensions = Array.AsReadOnly(extensions);
            }

            public IReadOnlyCollection<string> FileExtensions { get; }

            public string Name { get; }

            public FileFormatResult Format(FileFormatRequest request)
            {
                return new FileFormatResult(FileFormatStatus.Unchanged);
            }
        }
    }
}
