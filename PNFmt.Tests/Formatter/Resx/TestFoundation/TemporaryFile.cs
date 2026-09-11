namespace PNFmt.Tests.Formatter.Resx.TestFoundation
{
    using System;
    using System.IO;

    internal sealed class TemporaryFile : IDisposable
    {
        private readonly TestDirectory directory;

        private TemporaryFile(TestDirectory directory, string path)
        {
            this.directory = directory;
            this.Path = path;
        }

        public string DirectoryPath => this.directory.Path;
        public string Path { get; }

        public static TemporaryFile Create(string contents, string extension = ".resx")
        {
            var directory = new TestDirectory();
            var path = directory.Write("input" + extension, contents);
            return new TemporaryFile(directory, path);
        }

        public static TemporaryFile Copy(string sourcePath)
        {
            var extension = System.IO.Path.GetExtension(sourcePath);
            var directory = new TestDirectory();
            var path = directory.GetPath("input" + extension);
            File.Copy(sourcePath, path);
            return new TemporaryFile(directory, path);
        }

        public void Dispose()
        {
            this.directory.Dispose();
        }
    }
}
