using System;

namespace PNFmt.Cli
{
    internal static class PathComparison
    {
        // Unix paths may identify distinct files when only their casing differs.
        public static StringComparer Comparer { get; } = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }
}
