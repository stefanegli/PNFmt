// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace CsProjFormatter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public interface IItemSortingSettings
    {
        IReadOnlyCollection<string> SortItemTypes { get; }
    }

    public static class ItemSortingSettings
    {
        private static readonly string[] DefaultTypes =
        {
            "AdditionalFiles",
            "Analyzer",
            "ApplicationDefinition",
            "AssemblyAttribute",
            "AssemblyMetadata",
            "Compile",
            "CompilerVisibleItemMetadata",
            "CompilerVisibleProperty",
            "COMFileReference",
            "COMReference",
            "Content",
            "EditorConfigFiles",
            "EmbeddedResource",
            "Folder",
            "FrameworkReference",
            "GlobalAnalyzerConfigFiles",
            "InternalsVisibleTo",
            "NativeReference",
            "None",
            "PackageDownload",
            "PackageReference",
            "Page",
            "ProjectReference",
            "PrunePackageReference",
            "Reference",
            "Resource",
            "RuntimeHostConfigurationOption",
            "SplashScreen",
            "TrimmerRootAssembly",
            "Using",
        };

        private static readonly IReadOnlyCollection<string> ReadOnlyDefaultTypes = Array.AsReadOnly(DefaultTypes);

        public static IReadOnlyCollection<string> Defaults => ReadOnlyDefaultTypes;

        internal static HashSet<string> Resolve(ISettings settings)
        {
            var configuredSettings = settings as IItemSortingSettings;
            var itemTypes = configuredSettings?.SortItemTypes;
            if (itemTypes is null || itemTypes.Count == 0)
            {
                itemTypes = Defaults;
            }

            return new HashSet<string>(
                itemTypes.Where(x => !string.IsNullOrWhiteSpace(x)),
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
