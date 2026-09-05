using System;

namespace PNFmt
{
    internal sealed class ResxEditorConfigSettings : IResxFormatSettings
    {
        public ResxEditorConfigSettings(IFormatterLog log, string targetFile = "dummy.resx")
        {
            var isActive = false;
            var settings = EditorConfigSettings.Load(targetFile, log);
            var resolver = new EditorConfigSettingResolver(settings, targetFile, log);
            if (resolver.TryGet(
                EditorConfigSettingNames.SortEntries,
                "resx_formatter_sort_entries",
                out var sortEntries))
            {
                isActive = true;
                this.SortEntries = EditorConfigSettings.IsEnabled(sortEntries);
            }

            if (resolver.TryGet(
                EditorConfigSettingNames.ResxRemoveXsdSchema,
                "resx_formatter_remove_xsd_schema",
                out var removeSchema))
            {
                isActive = true;
                this.RemoveXsdSchema = EditorConfigSettings.IsEnabled(removeSchema);
            }

            if (resolver.TryGet(
                EditorConfigSettingNames.ResxRemoveDocumentationComment,
                "resx_formatter_remove_documentation_comment",
                out var removeComment))
            {
                isActive = true;
                this.RemoveDocumentationComment = EditorConfigSettings.IsEnabled(removeComment);
            }

            if (resolver.TryGet(
                    EditorConfigSettingNames.ResxSortComparer,
                    "resx_formatter_sort_comparer",
                    out var comparerString)
                && this.SortEntries)
            {
                this.Comparer = Comparer(comparerString);
            }

            this.IsActive = isActive;

            StringComparer Comparer(string comparerString)
            {
                switch (comparerString)
                {
                    case nameof(StringComparer.InvariantCulture): return StringComparer.InvariantCulture;
                    case nameof(StringComparer.InvariantCultureIgnoreCase): return StringComparer.InvariantCultureIgnoreCase;
                    case nameof(StringComparer.OrdinalIgnoreCase): return StringComparer.OrdinalIgnoreCase;
                    default: return StringComparer.Ordinal;
                }
            }
        }

        public StringComparer Comparer { get; private set; } = StringComparer.Ordinal;
        public bool IsActive { get; }
        public bool RemoveDocumentationComment { get; }
        public bool RemoveXsdSchema { get; }
        public bool SortEntries { get; }
    }
}
