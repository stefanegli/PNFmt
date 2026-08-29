// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace CsProjFormatter
{
    using System;
    using System.Collections.Generic;

    internal static class ProjectStructure
    {
        public static readonly HashSet<string> KnownTopLevelElements =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Choose",
                "Import",
                "ImportGroup",
                "ItemDefinitionGroup",
                "ItemGroup",
                "ProjectExtensions",
                "PropertyGroup",
                "Sdk",
                "Target",
                "UsingTask",
            };
    }
}
