// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace PNFmt
{
    public sealed class FormatterProviderRegistry
    {
        private readonly IReadOnlyDictionary<string, IFileFormatterProvider> providersByExtension;

        public FormatterProviderRegistry(IEnumerable<IFileFormatterProvider> providers)
        {
            if (providers is null)
            {
                throw new ArgumentNullException(nameof(providers));
            }

            var providerList = new List<IFileFormatterProvider>();
            var providerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var extensionMap = new Dictionary<string, IFileFormatterProvider>(StringComparer.OrdinalIgnoreCase);

            foreach (var provider in providers)
            {
                if (provider is null)
                {
                    throw new ArgumentException("The provider collection cannot contain null.", nameof(providers));
                }

                if (string.IsNullOrWhiteSpace(provider.Name))
                {
                    throw new ArgumentException("Every provider must have a name.", nameof(providers));
                }

                if (!providerNames.Add(provider.Name))
                {
                    throw new InvalidOperationException(
                        $"The formatter provider name '{provider.Name}' is registered more than once.");
                }

                if (provider.FileExtensions is null || provider.FileExtensions.Count == 0)
                {
                    throw new ArgumentException(
                        $"Formatter provider '{provider.Name}' must register at least one file extension.",
                        nameof(providers));
                }

                foreach (var extension in provider.FileExtensions)
                {
                    var normalizedExtension = NormalizeExtension(extension, provider.Name);
                    if (extensionMap.ContainsKey(normalizedExtension))
                    {
                        var existingProvider = extensionMap[normalizedExtension];
                        throw new InvalidOperationException(
                            $"The file extension '{normalizedExtension}' is registered by both "
                            + $"'{existingProvider.Name}' and '{provider.Name}'.");
                    }

                    extensionMap.Add(normalizedExtension, provider);
                }

                providerList.Add(provider);
            }

            if (providerList.Count == 0)
            {
                throw new ArgumentException("At least one formatter provider is required.", nameof(providers));
            }

            this.Providers = new ReadOnlyCollection<IFileFormatterProvider>(providerList);
            this.providersByExtension = new ReadOnlyDictionary<string, IFileFormatterProvider>(extensionMap);
            this.SupportedExtensions = new ReadOnlyCollection<string>(
                extensionMap.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList());
        }

        public IReadOnlyCollection<IFileFormatterProvider> Providers { get; }

        public IReadOnlyCollection<string> SupportedExtensions { get; }

        public bool TryGetProvider(string filePath, out IFileFormatterProvider provider)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                provider = null;
                return false;
            }

            return this.providersByExtension.TryGetValue(Path.GetExtension(filePath), out provider);
        }

        private static string NormalizeExtension(string extension, string providerName)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                throw new ArgumentException(
                    $"Formatter provider '{providerName}' contains an empty file extension.",
                    nameof(extension));
            }

            var normalized = extension.Trim();
            if (!normalized.StartsWith(".", StringComparison.Ordinal))
            {
                normalized = "." + normalized;
            }

            if (normalized.Length == 1
                || normalized.IndexOfAny(new[] { '\\', '/', '*', '?' }) >= 0)
            {
                throw new ArgumentException(
                    $"Formatter provider '{providerName}' contains an invalid file extension '{extension}'.",
                    nameof(extension));
            }

            return normalized.ToLowerInvariant();
        }
    }
}

