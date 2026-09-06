// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using System.Text.Json;

namespace PNFmt.Cli
{
    internal sealed class PNFmtConfiguration
    {
        private const int DefaultMaxCpuCount = 1;

        private PNFmtConfiguration(int maxCpuCount)
        {
            this.MaxCpuCount = maxCpuCount;
        }

        public int MaxCpuCount { get; }

        public static PNFmtConfiguration Load(string repositoryRoot)
        {
            if (string.IsNullOrEmpty(repositoryRoot))
            {
                return new PNFmtConfiguration(DefaultMaxCpuCount);
            }

            var path = Path.Combine(repositoryRoot, ".pnfmt");
            if (!File.Exists(path))
            {
                return new PNFmtConfiguration(DefaultMaxCpuCount);
            }

            try
            {
                using (var document = JsonDocument.Parse(File.ReadAllText(path)))
                {
                    if (document.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        throw Invalid(path, "The root value must be a JSON object.");
                    }

                    var maxCpuCount = DefaultMaxCpuCount;
                    var hasMaxCpuCount = false;
                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        if (!string.Equals(
                            property.Name,
                            "maxCpuCount",
                            StringComparison.Ordinal))
                        {
                            throw Invalid(path, $"Unknown setting '{property.Name}'.");
                        }

                        if (hasMaxCpuCount)
                        {
                            throw Invalid(path, "Setting 'maxCpuCount' appears more than once.");
                        }

                        hasMaxCpuCount = true;
                        if (!property.Value.TryGetInt32(out maxCpuCount) || maxCpuCount <= 0)
                        {
                            throw Invalid(path, "Setting 'maxCpuCount' must be a positive integer.");
                        }
                    }

                    return new PNFmtConfiguration(maxCpuCount);
                }
            }
            catch (PNFmtConfigurationException)
            {
                throw;
            }
            catch (Exception ex) when (ex is JsonException
                || ex is IOException
                || ex is UnauthorizedAccessException)
            {
                throw new PNFmtConfigurationException(
                    $"Unable to read PNFmt configuration '{path}': {ex.Message}",
                    ex);
            }
        }

        private static PNFmtConfigurationException Invalid(string path, string message)
        {
            return new PNFmtConfigurationException(
                $"Invalid PNFmt configuration '{path}': {message}");
        }
    }

    internal sealed class PNFmtConfigurationException : Exception
    {
        public PNFmtConfigurationException(string message)
            : base(message)
        {
        }

        public PNFmtConfigurationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
