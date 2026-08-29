// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;

namespace PNFmt.Tests.Snapshots
{
    internal sealed class TemporarySnapshotDirectory : IDisposable
    {
        private TemporarySnapshotDirectory(string path)
        {
            this.Path = path;
        }

        public string Path { get; }

        public static TemporarySnapshotDirectory CopyFrom(string sourcePath)
        {
            if (!Directory.Exists(sourcePath))
            {
                throw new DirectoryNotFoundException($"Snapshot source directory not found: {sourcePath}");
            }

            var destinationPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "PNFmtSnapshotTests",
                Guid.NewGuid().ToString("N"));
            CopyDirectory(sourcePath, destinationPath);
            return new TemporarySnapshotDirectory(destinationPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(this.Path))
            {
                Directory.Delete(this.Path, true);
            }
        }

        public string GetPath(string relativePath)
        {
            return System.IO.Path.Combine(this.Path, relativePath);
        }

        private static void CopyDirectory(string sourcePath, string destinationPath)
        {
            Directory.CreateDirectory(destinationPath);

            foreach (var directory in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                var relativePath = System.IO.Path.GetRelativePath(sourcePath, directory);
                Directory.CreateDirectory(System.IO.Path.Combine(destinationPath, relativePath));
            }

            foreach (var file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
            {
                var relativePath = System.IO.Path.GetRelativePath(sourcePath, file);
                var destinationFile = System.IO.Path.Combine(destinationPath, relativePath);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destinationFile));
                File.Copy(file, destinationFile);
            }
        }
    }
}
