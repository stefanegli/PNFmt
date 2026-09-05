// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using System.Text;
using PNFmt;

namespace PNFmt.Cli
{
    internal sealed class DefaultEditorConfigWriter
    {
        private readonly string original;

        private DefaultEditorConfigWriter(string path, string original)
        {
            this.TargetPath = path;
            this.original = original;
            this.LegacySettingCount = DefaultEditorConfigDocument.CountLegacySettings(original);
        }

        public int LegacySettingCount { get; }

        public string TargetPath { get; }

        public static DefaultEditorConfigWriter Open(string targetPath)
        {
            var editorConfigPath = ResolveEditorConfigPath(targetPath);
            var original = File.Exists(editorConfigPath)
                ? File.ReadAllText(editorConfigPath)
                : string.Empty;
            return new DefaultEditorConfigWriter(editorConfigPath, original);
        }

        public DefaultEditorConfigWriteResult Write(
            bool migrateLegacySettings,
            bool removeLegacySettings)
        {
            var updated = DefaultEditorConfigDocument.Update(
                this.original,
                migrateLegacySettings,
                removeLegacySettings);
            if (string.Equals(this.original, updated, StringComparison.Ordinal))
            {
                return new DefaultEditorConfigWriteResult(this.TargetPath, false);
            }

            File.WriteAllText(this.TargetPath, updated, new UTF8Encoding(false));
            return new DefaultEditorConfigWriteResult(this.TargetPath, true);
        }

        private static string ResolveEditorConfigPath(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                throw new ArgumentException("A target path is required.", nameof(targetPath));
            }

            var fullPath = Path.GetFullPath(targetPath);
            if (Directory.Exists(fullPath))
            {
                return Path.Combine(fullPath, ".editorconfig");
            }

            if (!string.Equals(
                Path.GetFileName(fullPath),
                ".editorconfig",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "The default configuration target must be a directory or an .editorconfig file.",
                    nameof(targetPath));
            }

            var parentDirectory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(parentDirectory) || !Directory.Exists(parentDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"The parent directory does not exist: {parentDirectory}");
            }

            return fullPath;
        }
    }

    internal sealed class DefaultEditorConfigWriteResult
    {
        public DefaultEditorConfigWriteResult(string path, bool changed)
        {
            this.Path = path;
            this.Changed = changed;
        }

        public bool Changed { get; }

        public string Path { get; }
    }
}
