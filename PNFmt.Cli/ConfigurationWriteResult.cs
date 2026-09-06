// Copyright (c) 2026 by Stefan Egli. All rights reserved.

namespace PNFmt.Cli
{
    internal sealed class ConfigurationWriteResult
    {
        public ConfigurationWriteResult(string path, bool changed)
        {
            this.Path = path;
            this.Changed = changed;
        }

        public bool Changed { get; }

        public string Path { get; }
    }
}
