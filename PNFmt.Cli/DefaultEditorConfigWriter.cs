// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using System.Text;

using PNFmt;

namespace PNFmt.Cli
{
    internal sealed class DefaultEditorConfigWriter
    {
        private readonly EncodedTextFile original;

        private DefaultEditorConfigWriter(string path, EncodedTextFile original)
        {
            this.TargetPath = path;
            this.original = original;
            this.LegacySettingCount = DefaultEditorConfigDocument.CountLegacySettings(original?.Text ?? string.Empty);
        }

        public int LegacySettingCount { get; }

        public string TargetPath { get; }

        public static DefaultEditorConfigWriter Open(string targetPath)
        {
            var editorConfigPath = ResolveEditorConfigPath(targetPath);
            var original = File.Exists(editorConfigPath)
                ? EncodedTextFile.Read(editorConfigPath)
                : null;
            return new DefaultEditorConfigWriter(editorConfigPath, original);
        }

        public ConfigurationWriteResult Write(
            bool migrateLegacySettings,
            bool removeLegacySettings)
        {
            var originalText = this.original?.Text ?? string.Empty;
            var updated = DefaultEditorConfigDocument.Update(
                originalText,
                migrateLegacySettings,
                removeLegacySettings);
            if (string.Equals(originalText, updated, StringComparison.Ordinal))
            {
                return new ConfigurationWriteResult(this.TargetPath, false);
            }

            if (this.original is not null)
            {
                this.original.Write(this.TargetPath, new UTF8Encoding(false, true).GetBytes(updated));
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
