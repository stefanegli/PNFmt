using System;

namespace PNFmt
{
    internal sealed class ResxEditorConfigSettings : IResxFormatSettings
    {
        public ResxEditorConfigSettings(IFormatterLog log, string targetFile = "dummy.resx")
        {
            var isActive = false;
            var settings = EditorConfigSettings.Load(targetFile);
            var legacy = EditorConfigFormatterActivation.GetSelection(settings) is null
                && EditorConfigFormatterActivation.GetEnablement(settings) is null;
            this.FormatLayout = EditorConfigFormatterOptions.Format(settings, "resx");
            this.HasExplicitLayout = settings.ContainsKey(EditorConfigSettingNames.Format) || !legacy;
            this.InsertDocumentationComment = settings.TryGetValue(EditorConfigSettingNames.ResxInsertDocumentationComment, out var insertComment)
                ? EditorConfigSettings.IsEnabled(insertComment) : legacy;
            this.InsertXsdSchema = settings.TryGetValue(EditorConfigSettingNames.ResxInsertXsdSchema, out var insertSchema)
                ? EditorConfigSettings.IsEnabled(insertSchema) : legacy;
            this.Layout = new ResxLayoutSettings(settings);
            var resolver = new EditorConfigSettingResolver(settings, targetFile, log);
            if (resolver.TryGet(
                LegacyEditorConfigSettingAliases.ResxSortEntries,
                out var sortEntries))
            {
                isActive = true;
                this.SortEntries = EditorConfigSettings.IsEnabled(sortEntries);
            }

            if (resolver.TryGet(
                LegacyEditorConfigSettingAliases.ResxRemoveXsdSchema,
                out var removeSchema))
            {
                isActive = true;
                this.RemoveXsdSchema = EditorConfigSettings.IsEnabled(removeSchema);
            }

            if (resolver.TryGet(
                LegacyEditorConfigSettingAliases.ResxRemoveDocumentationComment,
                out var removeComment))
            {
                isActive = true;
                this.RemoveDocumentationComment = EditorConfigSettings.IsEnabled(removeComment);
            }

            if (resolver.TryGet(
                    LegacyEditorConfigSettingAliases.ResxSortComparer,
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
        public bool FormatLayout { get; }
        public bool HasExplicitLayout { get; }
        public bool InsertDocumentationComment { get; }
        public bool InsertXsdSchema { get; }
        public ResxLayoutSettings Layout { get; }
        public bool RemoveDocumentationComment { get; }
        public bool RemoveXsdSchema { get; }
        public bool SortEntries { get; }
    }
}
