// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace PNFmt.Cli
{
    internal sealed class CommandLineOptions
    {
        private CommandLineOptions(
            bool recursive,
            bool verbose,
            bool dryRun,
            bool check,
            bool lint,
            bool showHelp,
            bool showVersion,
            int maxCpuCount,
            IReadOnlyList<string> filePatterns,
            IReadOnlyList<string> formatterNames,
            IReadOnlyList<string> paths)
        {
            this.Recursive = recursive;
            this.Verbose = verbose;
            this.DryRun = dryRun;
            this.Check = check;
            this.Lint = lint;
            this.ShowHelp = showHelp;
            this.ShowVersion = showVersion;
            this.MaxCpuCount = maxCpuCount;
            this.FilePatterns = filePatterns;
            this.FormatterNames = formatterNames;
            this.Paths = paths;
        }

        public bool Check { get; }

        public bool DryRun { get; }

        public IReadOnlyList<string> FilePatterns { get; }

        public IReadOnlyList<string> FormatterNames { get; }

        public bool Lint { get; }

        public int MaxCpuCount { get; }

        public IReadOnlyList<string> Paths { get; }

        public bool Recursive { get; }

        public bool ShowHelp { get; }

        public bool ShowVersion { get; }

        public bool Verbose { get; }

        public static CommandLineOptions Parse(string[] args)
        {
            var recursive = false;
            var verbose = false;
            var dryRun = false;
            var check = false;
            var lint = false;
            var stopOptions = false;
            var maxCpuCount = 1;
            var filePatterns = new List<string>();
            var formatterNames = new List<string>();
            var paths = new List<string>();
            var arguments = args ?? Array.Empty<string>();

            for (var index = 0; index < arguments.Length; index++)
            {
                var arg = arguments[index];
                if (!stopOptions && string.Equals(arg, "--", StringComparison.Ordinal))
                {
                    stopOptions = true;
                    continue;
                }

                if (!stopOptions && TryGetMaxCpuCountValue(arg, out var maxCpuCountValue))
                {
                    if (maxCpuCountValue is null)
                    {
                        maxCpuCount = Math.Max(1, Environment.ProcessorCount);
                    }
                    else if (!TryParseMaxCpuCount(maxCpuCountValue, out maxCpuCount))
                    {
                        throw new CommandLineException(
                            $"Option '{arg}' requires a positive integer after ':'.");
                    }

                    continue;
                }

                if (!stopOptions
                    && TryReadOptionValue(
                        arguments,
                        ref index,
                        arg,
                        "file pattern",
                        out var filePattern,
                        "--file-pattern",
                        "--filepattern"))
                {
                    filePatterns.Add(filePattern);
                    continue;
                }

                if (!stopOptions
                    && TryReadOptionValue(
                        arguments,
                        ref index,
                        arg,
                        "formatter",
                        out var formatterValue,
                        "--formatter",
                        "--formatters"))
                {
                    AddFormatterNames(formatterValue, arg, formatterNames);
                    continue;
                }

                if (!stopOptions && IsHelpArg(arg))
                {
                    return Create(
                        recursive,
                        verbose,
                        dryRun,
                        check,
                        lint,
                        showHelp: true,
                        showVersion: false,
                        maxCpuCount,
                        filePatterns,
                        formatterNames,
                        paths);
                }

                if (!stopOptions && IsVersionArg(arg))
                {
                    return Create(
                        recursive,
                        verbose,
                        dryRun,
                        check,
                        lint,
                        showHelp: false,
                        showVersion: true,
                        maxCpuCount,
                        filePatterns,
                        formatterNames,
                        paths);
                }

                if (!stopOptions && (string.Equals(arg, "-r", StringComparison.Ordinal)
                    || string.Equals(arg, "--recursive", StringComparison.Ordinal)))
                {
                    recursive = true;
                    continue;
                }

                if (!stopOptions && (string.Equals(arg, "-v", StringComparison.Ordinal)
                    || string.Equals(arg, "--verbose", StringComparison.Ordinal)))
                {
                    verbose = true;
                    continue;
                }

                if (!stopOptions && (string.Equals(arg, "-n", StringComparison.Ordinal)
                    || string.Equals(arg, "--dry-run", StringComparison.Ordinal)))
                {
                    dryRun = true;
                    continue;
                }

                if (!stopOptions && string.Equals(arg, "--check", StringComparison.Ordinal))
                {
                    check = true;
                    dryRun = true;
                    continue;
                }

                if (!stopOptions && string.Equals(arg, "--lint", StringComparison.Ordinal))
                {
                    lint = true;
                    check = true;
                    dryRun = true;
                    continue;
                }

                if (!stopOptions && arg.StartsWith("-", StringComparison.Ordinal))
                {
                    throw new CommandLineException($"Unknown option: {arg}");
                }

                paths.Add(arg);
            }

            if (paths.Count == 0)
            {
                paths.Add(".");
            }

            return Create(
                recursive,
                verbose,
                dryRun,
                check,
                lint,
                showHelp: false,
                showVersion: false,
                maxCpuCount,
                filePatterns,
                formatterNames,
                paths);
        }

        private static void AddFormatterNames(
            string value,
            string option,
            List<string> formatterNames)
        {
            var names = value.Split(new[] { ',' }, StringSplitOptions.None);
            foreach (var name in names)
            {
                var trimmedName = name.Trim();
                if (trimmedName.Length == 0)
                {
                    throw new CommandLineException(
                        $"Option '{option}' contains an empty formatter name.");
                }

                formatterNames.Add(trimmedName);
            }
        }

        private static CommandLineOptions Create(
            bool recursive,
            bool verbose,
            bool dryRun,
            bool check,
            bool lint,
            bool showHelp,
            bool showVersion,
            int maxCpuCount,
            List<string> filePatterns,
            List<string> formatterNames,
            List<string> paths)
        {
            return new CommandLineOptions(
                recursive,
                verbose,
                dryRun,
                check,
                lint,
                showHelp,
                showVersion,
                maxCpuCount,
                filePatterns.ToArray(),
                formatterNames.ToArray(),
                paths.ToArray());
        }

        private static bool IsHelpArg(string arg)
        {
            return string.Equals(arg, "-h", StringComparison.Ordinal)
                || string.Equals(arg, "--help", StringComparison.Ordinal)
                || string.Equals(arg, "/?", StringComparison.Ordinal);
        }

        private static bool IsVersionArg(string arg)
        {
            return string.Equals(arg, "-V", StringComparison.Ordinal)
                || string.Equals(arg, "--version", StringComparison.Ordinal);
        }

        private static bool TryGetMaxCpuCountValue(string arg, out string value)
        {
            const string ShortOption = "-m";
            const string LongOption = "-maxCpuCount";
            if (string.Equals(arg, ShortOption, StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, LongOption, StringComparison.OrdinalIgnoreCase))
            {
                value = null;
                return true;
            }

            var shortPrefix = ShortOption + ":";
            if (arg.StartsWith(shortPrefix, StringComparison.OrdinalIgnoreCase))
            {
                value = arg.Substring(shortPrefix.Length);
                return true;
            }

            var longPrefix = LongOption + ":";
            if (arg.StartsWith(longPrefix, StringComparison.OrdinalIgnoreCase))
            {
                value = arg.Substring(longPrefix.Length);
                return true;
            }

            value = null;
            return false;
        }

        private static bool TryParseMaxCpuCount(string value, out int maxCpuCount)
        {
            return int.TryParse(
                    value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out maxCpuCount)
                && maxCpuCount > 0;
        }

        private static bool TryReadOptionValue(
            IReadOnlyList<string> arguments,
            ref int index,
            string arg,
            string optionDescription,
            out string value,
            params string[] optionNames)
        {
            value = null;
            foreach (var optionName in optionNames)
            {
                if (string.Equals(arg, optionName, StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 >= arguments.Count
                        || arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
                    {
                        throw new CommandLineException(
                            $"Option '{arg}' requires a {optionDescription}.");
                    }

                    value = arguments[++index];
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        throw new CommandLineException(
                            $"Option '{arg}' requires a {optionDescription}.");
                    }

                    return true;
                }

                foreach (var separator in new[] { ":", "=" })
                {
                    var prefix = optionName + separator;
                    if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        value = arg.Substring(prefix.Length);
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            throw new CommandLineException(
                                $"Option '{arg}' requires a {optionDescription}.");
                        }

                        return true;
                    }
                }
            }

            return false;
        }
    }

    internal sealed class CommandLineException : Exception
    {
        public CommandLineException(string message)
            : base(message)
        {
        }
    }
}
