// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;

namespace PNFmt
{
    internal enum FormatterLogMessageKind
    {
        Detail,
        Progress,
        Warning,
    }

    internal sealed class FormatterLogMessage
    {
        private FormatterLogMessage(FormatterLogMessageKind kind, string message, string file = null, string code = null)
        {
            this.Kind = kind;
            this.Message = message ?? throw new ArgumentNullException(nameof(message));
            this.File = file;
            this.Code = code;
        }

        public FormatterLogMessageKind Kind { get; }
        public string Message { get; }
        public string File { get; }
        public string Code { get; }

        public static FormatterLogMessage Detail(string message) => new FormatterLogMessage(FormatterLogMessageKind.Detail, message);
        public static FormatterLogMessage Progress(string message) => new FormatterLogMessage(FormatterLogMessageKind.Progress, message);
        public override string ToString() => this.Kind == FormatterLogMessageKind.Warning
            ? $"{this.File}: warning {this.Code}: {this.Message}" : this.Message;

        public static FormatterLogMessage Warning(string file, string code, string message)
            => new FormatterLogMessage(FormatterLogMessageKind.Warning, message, file, code);
    }

    internal interface IFormatterMessageLog : IFormatterLog
    {
        void Write(FormatterLogMessage message);
    }

    internal static class FormatterLog
    {
        public static void Progress(this IFormatterLog log, string message)
            => Write(log, FormatterLogMessage.Progress(message));

        public static void Warning(this IFormatterLog log, string file, string code, string message)
            => Write(log, FormatterLogMessage.Warning(file, code, message));

        private static void Write(IFormatterLog log, FormatterLogMessage message)
        {
            // The CLI retains meaning until reporting. Existing text-log adapters
            // keep the public IFormatterLog contract and its familiar text output.
            if (log is IFormatterMessageLog structured)
            {
                structured.Write(message);
            }
            else
            {
                log?.WriteLine(message.ToString());
            }
        }
    }
}
