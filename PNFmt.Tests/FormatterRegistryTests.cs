// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using Xunit;

namespace PNFmt.Tests
{
    public sealed class FormatterRegistryTests
    {
        [Fact]
        public void One_formatter_can_handle_multiple_extensions()
        {
            var formatter = new TestFormatter("resource", "markup", ".xml");
            var registry = new FormatterRegistry(new[] { formatter });

            Assert.True(registry.TryGetFormatter("settings.MARKUP", out var markupFormatter));
            Assert.True(registry.TryGetFormatter("settings.xml", out var xmlFormatter));
            Assert.Same(formatter, markupFormatter);
            Assert.Same(formatter, xmlFormatter);
        }

        [Fact]
        public void Duplicate_extensions_are_rejected()
        {
            var first = new TestFormatter("first", ".xml");
            var second = new TestFormatter("second", "xml");

            Assert.Throws<InvalidOperationException>(
                () => new FormatterRegistry(new[] { first, second }));
        }

        [Fact]
        public void Default_catalog_registers_the_current_file_types()
        {
            var registry = FormatterCatalog.CreateDefault();

            Assert.True(registry.TryGetFormatter("Project.CSPROJ", out var csprojFormatter));
            Assert.True(registry.TryGetFormatter(".EDITORCONFIG", out var editorConfigFormatter));
            Assert.True(registry.TryGetFormatter("settings.INI", out var iniFormatter));
            Assert.True(registry.TryGetFormatter("Strings.RESX", out var resxFormatter));
            Assert.True(registry.TryGetFormatter("Solution.SLNX", out var slnxFormatter));
            Assert.Equal("csproj", csprojFormatter.Name);
            Assert.Equal("ini", editorConfigFormatter.Name);
            Assert.Same(editorConfigFormatter, iniFormatter);
            Assert.Equal("resx", resxFormatter.Name);
            Assert.Equal("slnx", slnxFormatter.Name);
        }

        private sealed class TestFormatter : IFileFormatter
        {
            public TestFormatter(string name, params string[] extensions)
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
