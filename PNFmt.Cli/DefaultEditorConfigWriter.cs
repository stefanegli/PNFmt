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
        private readonly bool targetExisted;

        private DefaultEditorConfigWriter(string path, string original, bool targetExisted)
        {
            this.TargetPath = path;
            this.original = original;
            this.targetExisted = targetExisted;
            this.LegacySettingCount = DefaultEditorConfigDocument.CountLegacySettings(original);
        }

        public int LegacySettingCount { get; }

        public string TargetPath { get; }

        public static DefaultEditorConfigWriter Open(string targetPath)
        {
            var editorConfigPath = ResolveEditorConfigPath(targetPath);
            var targetExisted = File.Exists(editorConfigPath);
            var original = targetExisted
                ? File.ReadAllText(editorConfigPath)
                : string.Empty;
            return new DefaultEditorConfigWriter(editorConfigPath, original, targetExisted);
        }

        public ConfigurationWriteResult Write(
            bool migrateLegacySettings,
            bool removeLegacySettings)
        {
            var updated = DefaultEditorConfigDocument.Update(
                this.original,
                migrateLegacySettings,
                removeLegacySettings);
            if (string.Equals(this.original, updated, StringComparison.Ordinal))
            {
                return new ConfigurationWriteResult(this.TargetPath, false);
            }

            if (this.targetExisted)
            {
                File.WriteAllText(this.TargetPath, updated, new UTF8Encoding(false));
            }
            else
            {
                try
                {
                    ConfigurationFileWriter.Create(this.TargetPath, updated);
                }
                catch (IOException ex) when (File.Exists(this.TargetPath))
                {
                    throw new IOException(
                        $"The EditorConfig file was created while defaults were being prepared: "
                        + this.TargetPath,
                        ex);
                }
            }

            return new ConfigurationWriteResult(this.TargetPath, true);
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

}
