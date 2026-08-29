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
    }
}

