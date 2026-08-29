// Copyright (c) 2026 by Stefan Egli.All rights reserved

namespace CsProjFormatter
{
    using System;
    using System.Collections.Generic;

    public class ConfigurableCsProjFormatter
    {
        public ConfigurableCsProjFormatter(ILog log)
        {
            this.Log = log;
        }

        public bool IsActive { get; private set; }

        public bool IsFileChanged { get; private set; }

        public bool IsSkipped { get; private set; }

        public IReadOnlyList<FormatterDiagnostic> Diagnostics { get; private set; } =
            Array.Empty<FormatterDiagnostic>();

        private ILog Log { get; }

        /// <summary>
        /// Runs formatting if EditorConfig enables it for the target file.
        /// </summary>
        public void Run(string csprojPath)
        {
            this.Run(csprojPath, true);
        }

        public void Run(string csprojPath, bool writeChanges)
        {
            this.Run(csprojPath, writeChanges, false);
        }

        public void Run(string csprojPath, bool writeChanges, bool forceActive)
        {
            this.IsFileChanged = false;
            this.IsSkipped = false;
            this.Diagnostics = Array.Empty<FormatterDiagnostic>();
            var settings = new CsProjEditorConfigSettings(csprojPath, this.Log);
            this.IsActive = settings.IsActive || forceActive;
            if (!this.IsActive)
            {
                return;
            }

            ISettings effectiveSettings = settings.IsActive ? settings : new Settings();
            var formatter = new CsProjFormatter(effectiveSettings, this.Log);
            var result = formatter.RunWithResult(csprojPath, writeChanges);
            this.IsFileChanged = result == FormatterRunResult.Updated;
            this.IsSkipped = result == FormatterRunResult.SkippedNonSdkStyle;
            this.Diagnostics = formatter.Diagnostics;
        }
    }
}
