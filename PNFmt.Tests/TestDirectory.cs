// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;

namespace PNFmt.Tests
{
    internal sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            this.Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PNFmtTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.Path);
        }

        public string Path { get; }

        public static TestDirectory CopyFrom(string sourcePath)
        {
            if (!Directory.Exists(sourcePath))
            {
                throw new DirectoryNotFoundException($"Test source directory not found: {sourcePath}");
            }

            var destination = new TestDirectory();
            try
            {
                foreach (var directory in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
                {
                    Directory.CreateDirectory(destination.GetPath(System.IO.Path.GetRelativePath(sourcePath, directory)));
                }

                foreach (var file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
                {
                    File.Copy(file, destination.GetPath(System.IO.Path.GetRelativePath(sourcePath, file)));
                }

                return destination;
            }
            catch
            {
                destination.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(this.Path))
            {
                // Git fixtures contain read-only objects on Windows.
                foreach (var file in Directory.GetFiles(this.Path, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(this.Path, true);
            }
        }

        public string GetPath(string relativePath) => System.IO.Path.Combine(this.Path, relativePath);

        public string Write(string relativePath, string contents)
        {
            var path = this.GetPath(relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            File.WriteAllText(path, contents);
            return path;
        }
    }
}
