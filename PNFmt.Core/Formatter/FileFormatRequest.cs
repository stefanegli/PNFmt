// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;

namespace PNFmt
{
    public sealed class FileFormatRequest
    {
        public FileFormatRequest(
            string filePath,
            bool writeChanges,
            bool lint,
            IFormatterLog log)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A file path is required.", nameof(filePath));
            }

            this.FilePath = filePath;
            this.WriteChanges = writeChanges;
            this.Lint = lint;
            this.Log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public string FilePath { get; }

        public bool Lint { get; }

        public IFormatterLog Log { get; }

        public bool WriteChanges { get; }

        internal FileFormattingConfiguration Configuration { get; private set; }

        internal FileFormatRequest ResolveConfiguration()
        {
            // Leave caller-owned requests reusable: the resolved copy belongs only
            // to this execution and can be shared by dispatch and formatting.
            return this.Configuration is not null ? this : new FileFormatRequest(this.FilePath, this.WriteChanges, this.Lint, this.Log)
            {
                Configuration = FileFormattingConfiguration.Load(this.FilePath, this.Log),
            };
        }
    }
}

