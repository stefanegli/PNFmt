// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace PNFmt
{
    public sealed class FormatterRegistry
    {
        private readonly IReadOnlyDictionary<string, IFileFormatter> formattersByExtension;

        public FormatterRegistry(IEnumerable<IFileFormatter> formatters)
        {
            if (formatters is null)
            {
                throw new ArgumentNullException(nameof(formatters));
            }

            var formatterList = new List<IFileFormatter>();
            var formatterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var extensionMap = new Dictionary<string, IFileFormatter>(StringComparer.OrdinalIgnoreCase);

            foreach (var formatter in formatters)
            {
                if (formatter is null)
                {
                    throw new ArgumentException("The formatter collection cannot contain null.", nameof(formatters));
                }

                if (string.IsNullOrWhiteSpace(formatter.Name))
                {
                    throw new ArgumentException("Every formatter must have a name.", nameof(formatters));
                }

                if (!formatterNames.Add(formatter.Name))
                {
                    throw new InvalidOperationException(
                        $"The formatter name '{formatter.Name}' is registered more than once.");
                }

                if (formatter.FileExtensions is null || formatter.FileExtensions.Count == 0)
                {
                    throw new ArgumentException(
                        $"Formatter '{formatter.Name}' must register at least one file extension.",
                        nameof(formatters));
                }

                foreach (var extension in formatter.FileExtensions)
                {
                    var normalizedExtension = NormalizeExtension(extension, formatter.Name);
                    if (extensionMap.ContainsKey(normalizedExtension))
                    {
                        var existingFormatter = extensionMap[normalizedExtension];
                        throw new InvalidOperationException(
                            $"The file extension '{normalizedExtension}' is registered by both "
                            + $"'{existingFormatter.Name}' and '{formatter.Name}'.");
                    }

                    extensionMap.Add(normalizedExtension, formatter);
                }

                formatterList.Add(formatter);
            }

            if (formatterList.Count == 0)
            {
                throw new ArgumentException("At least one formatter is required.", nameof(formatters));
            }

            this.Formatters = new ReadOnlyCollection<IFileFormatter>(formatterList);
            this.formattersByExtension = new ReadOnlyDictionary<string, IFileFormatter>(extensionMap);
            this.SupportedExtensions = new ReadOnlyCollection<string>(
                extensionMap.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList());
        }

        public IReadOnlyCollection<IFileFormatter> Formatters { get; }

        public IReadOnlyCollection<string> SupportedExtensions { get; }

        public bool TryGetFormatter(string filePath, out IFileFormatter formatter)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                formatter = null;
                return false;
            }

            var extension = Path.GetExtension(filePath);
            if (this.formattersByExtension.TryGetValue(extension, out formatter))
            {
                return true;
            }

            var fileName = Path.GetFileName(filePath);
            return fileName.StartsWith(".", StringComparison.Ordinal)
                && this.formattersByExtension.TryGetValue(fileName, out formatter);
        }

        private static string NormalizeExtension(string extension, string formatterName)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                throw new ArgumentException(
                    $"Formatter '{formatterName}' contains an empty file extension.",
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
                    $"Formatter '{formatterName}' contains an invalid file extension '{extension}'.",
                    nameof(extension));
            }

            return normalized.ToLowerInvariant();
        }
    }
}
