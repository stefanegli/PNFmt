// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System.IO;
using System.Text;

namespace PNFmt.Cli
{
    internal static class ConfigurationFileWriter
    {
        public static void Create(string path, string contents)
        {
            using (var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(contents);
            }
        }
    }
}
