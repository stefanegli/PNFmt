// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PNFmt
{
    // One resolved hierarchy per file operation. Discovery can resolve earlier;
    // execution resolves again after any preceding .editorconfig writes.
    internal sealed class FileFormattingConfiguration
    {
        private readonly string targetFile;
        private readonly IFormatterLog log;
        private readonly Lazy<CsProjEditorConfigSettings> project;
        private readonly Lazy<IniEditorConfigSettings> ini;
        private readonly Lazy<ResxEditorConfigSettings> resource;

        private FileFormattingConfiguration(string targetFile, IFormatterLog log)
        {
            this.targetFile = targetFile;
            this.log = log;
            this.Properties = EditorConfigSettings.Load(Path.GetFullPath(targetFile));
            this.Enablement = TryGet(this.Properties, EditorConfigSettingNames.Enabled, out var enabled)
                ? EditorConfigSettings.IsEnabled(enabled) : (bool?)null;
            this.Selection = TryGet(this.Properties, EditorConfigSettingNames.Formatter, out var selection) ? selection : null;
            this.Encoding = ResolveEncoding(this.Properties);
            this.project = new Lazy<CsProjEditorConfigSettings>(() => new CsProjEditorConfigSettings(this));
            this.ini = new Lazy<IniEditorConfigSettings>(() => new IniEditorConfigSettings(this));
            this.resource = new Lazy<ResxEditorConfigSettings>(() => new ResxEditorConfigSettings(this));
        }

        public IReadOnlyDictionary<string, string> Properties { get; }
        public Encoding Encoding { get; }
        public bool IsLegacy => this.Enablement is null && this.Selection is null;
        private bool? Enablement { get; }
        private string Selection { get; }

        public ICsProjFormatSettings ProjectSettings => this.project.Value.IsActive || this.Selection is not null
            ? this.project.Value : new DefaultCsProjFormatSettings();
        public IniEditorConfigSettings IniSettings => this.ini.Value;
        public ResxEditorConfigSettings ResourceSettings => this.resource.Value;

        public static FileFormattingConfiguration Load(string targetFile, IFormatterLog log = null)
            => new FileFormattingConfiguration(targetFile, log);

        public EditorConfigSettingResolver CreateSettingResolver()
            => new EditorConfigSettingResolver(this.Properties, this.targetFile, this.log);

        public bool TryGetFormatter(FormatterRegistry registry, out IFileFormatter formatter)
        {
            formatter = null;
            if (this.Enablement == false || string.Equals(this.Selection, "None", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (this.Selection is null)
            {
                return registry.TryGetFormatter(this.targetFile, out formatter);
            }

            formatter = registry.Formatters.FirstOrDefault(
                item => string.Equals(item.Name, this.Selection, StringComparison.OrdinalIgnoreCase));
            if (formatter is null)
            {
                throw new InvalidDataException(
                    $"Unknown formatter '{this.Selection}' in EditorConfig setting '{EditorConfigSettingNames.Formatter}'. "
                    + $"Available formatters: {string.Join(", ", registry.Formatters.Select(item => item.Name))}, None.");
            }

            return true;
        }

        public bool IsActive(string formatterName, bool lint = false)
        {
            // Resolve these settings before activation, preserving legacy alias diagnostics.
            var implicitlyEnabled = formatterName switch
            {
                "csproj" => this.project.Value.IsActive || lint,
                "resx" => this.resource.Value.IsActive,
                "ini" => this.ini.Value.IsActive,
                "csharp" => EditorConfigSettings.IsEnabled(this.Properties, EditorConfigSettingNames.CSharpFormat),
                "xml" => EditorConfigSettings.IsEnabled(this.Properties, EditorConfigSettingNames.XmlFormat),
                "xaml" => EditorConfigSettings.IsEnabled(this.Properties, EditorConfigSettingNames.XamlFormat),
                "rsp" or "slnx" => EditorConfigSettings.IsEnabled(this.Properties, EditorConfigSettingNames.SortEntries),
                _ => false,
            };
            if (this.Enablement == false || (this.Selection is not null
                && !string.Equals(this.Selection, formatterName, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var active = this.Enablement == true || this.Selection is not null || implicitlyEnabled;
            if (active && (this.Enablement is null || this.Selection is null))
            {
                this.log?.WriteLine($"{this.targetFile}: warning PNFMT004: Implicit formatter activation is deprecated. "
                    + $"Set 'pnfmt_enabled = true' and 'pnfmt_formatter = {formatterName}' in the applicable "
                    + ".editorconfig section to keep processing, or 'pnfmt_enabled = false' to disable it. "
                    + "Missing enablement or formatter selection will disable processing in a future version.");
            }

            return active;
        }

        public bool FormatLayout(string formatterName) => FormatLayout(this.Properties, formatterName);

        public static bool FormatLayout(IReadOnlyDictionary<string, string> settings, string formatterName)
        {
            if (TryGet(settings, EditorConfigSettingNames.Format, out var format))
            {
                return EditorConfigSettings.IsEnabled(format);
            }

            var legacyName = formatterName == "csharp" ? EditorConfigSettingNames.CSharpFormat
                : formatterName == "xml" ? EditorConfigSettingNames.XmlFormat
                : formatterName == "xaml" ? EditorConfigSettingNames.XamlFormat : null;
            return legacyName is null || !TryGet(settings, legacyName, out format) || EditorConfigSettings.IsEnabled(format);
        }

        private static Encoding ResolveEncoding(IReadOnlyDictionary<string, string> settings)
        {
            settings.TryGetValue("charset", out var charset);
            return charset?.ToLowerInvariant() switch
            {
                "utf-8" => new UTF8Encoding(false, true),
                "utf-8-bom" => new UTF8Encoding(true, true),
                "utf-16le" => new UnicodeEncoding(false, true, true),
                "utf-16be" => new UnicodeEncoding(true, true, true),
                "latin1" => Encoding.GetEncoding(28591, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback),
                _ => null,
            };
        }

        private static bool TryGet(IReadOnlyDictionary<string, string> settings, string name, out string value)
            => settings.TryGetValue(name, out value) && !string.Equals(value, "unset", StringComparison.OrdinalIgnoreCase);
    }
}
