// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace PNFmt.Cli
{
    internal sealed class CommandLineOptions
    {
        private CommandLineOptions(
            bool allFiles,
            bool recursive,
            bool verbose,
            bool dryRun,
            bool check,
            bool lint,
            bool writeDefaultConfig,
            bool? migrateLegacyConfig,
            bool? removeLegacyConfig,
            bool showHelp,
            bool showVersion,
            int? maxCpuCount,
            IReadOnlyList<string> filePatterns,
            IReadOnlyList<string> formatterNames,
            IReadOnlyList<string> paths)
        {
            this.AllFiles = allFiles;
            this.Recursive = recursive;
            this.Verbose = verbose;
            this.DryRun = dryRun;
            this.Check = check;
            this.Lint = lint;
            this.WriteDefaultConfig = writeDefaultConfig;
            this.MigrateLegacyConfig = migrateLegacyConfig;
            this.RemoveLegacyConfig = removeLegacyConfig;
            this.ShowHelp = showHelp;
            this.ShowVersion = showVersion;
            this.MaxCpuCount = maxCpuCount;
            this.FilePatterns = filePatterns;
            this.FormatterNames = formatterNames;
            this.Paths = paths;
        }

        public bool AllFiles { get; }

        public bool Check { get; }

        public bool DryRun { get; }

        public IReadOnlyList<string> FilePatterns { get; }

        public IReadOnlyList<string> FormatterNames { get; }

        public bool Lint { get; }

        public int? MaxCpuCount { get; }

        public bool? MigrateLegacyConfig { get; }

        public IReadOnlyList<string> Paths { get; }

        public bool Recursive { get; }

        public bool? RemoveLegacyConfig { get; }

        public bool ShowHelp { get; }

        public bool ShowVersion { get; }

        public bool Verbose { get; }

        public bool WriteDefaultConfig { get; }

        public static CommandLineOptions Parse(string[] args)
        {
            return new ArgumentParser().Parse(args ?? Array.Empty<string>());
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
            bool allFiles,
            bool recursive,
            bool verbose,
            bool dryRun,
            bool check,
            bool lint,
            bool writeDefaultConfig,
            bool? migrateLegacyConfig,
            bool? removeLegacyConfig,
            bool showHelp,
            bool showVersion,
            int? maxCpuCount,
            List<string> filePatterns,
            List<string> formatterNames,
            List<string> paths)
        {
            return new CommandLineOptions(
                allFiles,
                recursive,
                verbose,
                dryRun,
                check,
                lint,
                writeDefaultConfig,
                migrateLegacyConfig,
                removeLegacyConfig,
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

        private static bool TryReadBooleanOptionValue(
            IReadOnlyList<string> arguments,
            ref int index,
            string arg,
            out bool value,
            string optionName)
        {
            value = false;
            if (!TryReadOptionValue(
                arguments,
                ref index,
                arg,
                "value of true or false",
                out var text,
                optionName))
            {
                return false;
            }

            if (!bool.TryParse(text, out value))
            {
                throw new CommandLineException(
                    $"Option '{optionName}' requires a value of true or false.");
            }

            return true;
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

        private sealed class ArgumentParser
        {
            private static readonly IReadOnlyDictionary<string, SwitchOption> Switches =
                new Dictionary<string, SwitchOption>(StringComparer.Ordinal)
                {
                    ["-r"] = SwitchOption.Recursive,
                    ["--recursive"] = SwitchOption.Recursive,
                    ["-a"] = SwitchOption.AllFiles,
                    ["--all"] = SwitchOption.AllFiles,
                    ["-v"] = SwitchOption.Verbose,
                    ["--verbose"] = SwitchOption.Verbose,
                    ["-n"] = SwitchOption.DryRun,
                    ["--dry-run"] = SwitchOption.DryRun,
                    ["--check"] = SwitchOption.Check,
                    ["--lint"] = SwitchOption.Lint,
                    ["--write-default-config"] = SwitchOption.WriteDefaultConfig,
                };

            private readonly List<string> filePatterns = new List<string>();
            private readonly List<string> formatterNames = new List<string>();
            private readonly List<string> paths = new List<string>();
            private bool allFiles;
            private bool recursive;
            private bool verbose;
            private bool dryRun;
            private bool check;
            private bool lint;
            private bool writeDefaultConfig;
            private bool? migrateLegacyConfig;
            private bool? removeLegacyConfig;
            private int? maxCpuCount;

            public CommandLineOptions Parse(string[] arguments)
            {
                var stopOptions = false;
                for (var index = 0; index < arguments.Length; index++)
                {
                    var arg = arguments[index];
                    if (stopOptions)
                    {
                        this.paths.Add(arg);
                        continue;
                    }

                    if (string.Equals(arg, "--", StringComparison.Ordinal))
                    {
                        stopOptions = true;
                        continue;
                    }

                    if (this.TryReadValueOption(arguments, ref index, arg))
                    {
                        continue;
                    }

                    // Help/version return the options collected so far, without
                    // validating combinations or reading later arguments.
                    if (IsHelpArg(arg))
                    {
                        return this.CreateOptions(showHelp: true);
                    }

                    if (IsVersionArg(arg))
                    {
                        return this.CreateOptions(showVersion: true);
                    }

                    if (this.TryReadSwitch(arg))
                    {
                        continue;
                    }

                    if (arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        throw new CommandLineException($"Unknown option: {arg}");
                    }

                    this.paths.Add(arg);
                }

                this.ValidateCombination();
                if (this.paths.Count == 0)
                {
                    this.paths.Add(".");
                }

                return this.CreateOptions();
            }

            private CommandLineOptions CreateOptions(bool showHelp = false, bool showVersion = false)
            {
                return Create(this.allFiles, this.recursive, this.verbose, this.dryRun, this.check,
                    this.lint, this.writeDefaultConfig, this.migrateLegacyConfig, this.removeLegacyConfig,
                    showHelp, showVersion, this.maxCpuCount, this.filePatterns, this.formatterNames, this.paths);
            }

            private static int ParseMaxCpuCount(string arg, string value)
            {
                if (value is null)
                {
                    return Math.Max(1, Environment.ProcessorCount);
                }

                if (!TryParseMaxCpuCount(value, out var parsed))
                {
                    throw new CommandLineException($"Option '{arg}' requires a positive integer after ':'.");
                }

                return parsed;
            }

            private bool TryReadSwitch(string arg)
            {
                if (!Switches.TryGetValue(arg, out var option))
                {
                    return false;
                }

                switch (option)
                {
                    case SwitchOption.Recursive: this.recursive = true; break;
                    case SwitchOption.AllFiles: this.allFiles = true; break;
                    case SwitchOption.Verbose: this.verbose = true; break;
                    case SwitchOption.DryRun: this.dryRun = true; break;
                    case SwitchOption.Check: this.check = true; this.dryRun = true; break;
                    case SwitchOption.Lint: this.lint = true; this.check = true; this.dryRun = true; break;
                    case SwitchOption.WriteDefaultConfig: this.writeDefaultConfig = true; break;
                }

                return true;
            }

            private bool TryReadValueOption(string[] arguments, ref int index, string arg)
            {
                if (TryGetMaxCpuCountValue(arg, out var value))
                {
                    this.maxCpuCount = ParseMaxCpuCount(arg, value);
                    return true;
                }

                if (TryReadOptionValue(arguments, ref index, arg, "file pattern", out var pattern,
                    "--file-pattern", "--filepattern"))
                {
                    this.filePatterns.Add(pattern);
                    return true;
                }

                if (TryReadOptionValue(arguments, ref index, arg, "formatter", out var formatter,
                    "--formatter", "--formatters"))
                {
                    AddFormatterNames(formatter, arg, this.formatterNames);
                    return true;
                }

                if (TryReadBooleanOptionValue(arguments, ref index, arg, out var migrate, "--migrate-legacy-config"))
                {
                    this.migrateLegacyConfig = migrate;
                    return true;
                }

                if (TryReadBooleanOptionValue(arguments, ref index, arg, out var remove, "--remove-legacy-config"))
                {
                    this.removeLegacyConfig = remove;
                    return true;
                }

                return false;
            }

            private void ValidateCombination()
            {
                if (!this.writeDefaultConfig && (this.migrateLegacyConfig.HasValue || this.removeLegacyConfig.HasValue))
                {
                    throw new CommandLineException(
                        "Options '--migrate-legacy-config' and '--remove-legacy-config' "
                        + "require '--write-default-config'.");
                }

                if (this.writeDefaultConfig && (this.allFiles || this.recursive || this.verbose || this.dryRun
                    || this.check || this.lint || this.filePatterns.Count > 0 || this.formatterNames.Count > 0
                    || this.paths.Count > 1))
                {
                    throw new CommandLineException(
                        "Option '--write-default-config' accepts at most one path and cannot be combined "
                        + "with formatting options.");
                }
            }

            private enum SwitchOption
            {
                Recursive,
                AllFiles,
                Verbose,
                DryRun,
                Check,
                Lint,
                WriteDefaultConfig,
            }
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
