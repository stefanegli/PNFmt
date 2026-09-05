// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using System.Text;
using PNFmt;

namespace PNFmt.Cli
{
    internal static class DefaultEditorConfigWriter
    {
        public static DefaultEditorConfigWriteResult Write(string targetPath)
        {
            var editorConfigPath = ResolveEditorConfigPath(targetPath);
            var original = File.Exists(editorConfigPath)
                ? File.ReadAllText(editorConfigPath)
                : string.Empty;
            var updated = DefaultEditorConfigDocument.Update(original);
            if (string.Equals(original, updated, StringComparison.Ordinal))
            {
                return new DefaultEditorConfigWriteResult(editorConfigPath, false);
            }

            File.WriteAllText(editorConfigPath, updated, new UTF8Encoding(false));
            return new DefaultEditorConfigWriteResult(editorConfigPath, true);
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
