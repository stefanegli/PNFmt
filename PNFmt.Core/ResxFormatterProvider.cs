// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using ResxCore = global::ResxFormatter;

namespace PNFmt
{
    public sealed class ResxFormatterProvider : IFileFormatterProvider
    {
        private static readonly IReadOnlyCollection<string> Extensions =
            Array.AsReadOnly(new[] { ".resx" });

        public IReadOnlyCollection<string> FileExtensions => Extensions;

        public string Name => "resx";

        public FileFormatResult Format(FileFormatRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var formatter = new ResxCore.ConfigurableResxFormatter(new LogAdapter(request.Log));
            formatter.Run(request.FilePath, request.WriteChanges);

            var status = !formatter.IsActive
                ? FileFormatStatus.Skipped
                : formatter.IsFileChanged
                    ? FileFormatStatus.Updated
                    : FileFormatStatus.Unchanged;
            return new FileFormatResult(status);
        }

        private sealed class LogAdapter : ResxCore.ILog
        {
            private readonly IFormatterLog log;

            public LogAdapter(IFormatterLog log)
            {
                this.log = log;
                this.IsActive = true;
            }

            public bool IsActive { get; set; }

            public void Write(Exception exception)
            {
                this.log.Write(exception);
            }

            public void WriteLine(string message)
            {
                this.log.WriteLine(message);
            }
        }
    }
}

