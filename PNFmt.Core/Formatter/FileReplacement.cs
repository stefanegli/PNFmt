// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;

namespace PNFmt
{
    internal static class FileReplacement
    {
        public static void Write(string path, byte[] expectedBytes, Action<Stream> writeOutput)
        {
            path = Path.GetFullPath(path);
            EnsureWritableFile(path);
            var temporary = Path.Combine(Path.GetDirectoryName(path), ".pnfmt-" + Guid.NewGuid().ToString("N") + ".tmp");
            var backup = temporary + ".bak";
            var keepRecoveryFiles = false;

            // Reserve our own name before copying metadata. Copying the source
            // also retains Unix permission bits without a platform-specific API.
            using (new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            try
            {
                File.Copy(path, temporary, overwrite: true);
                using (var output = new FileStream(temporary, FileMode.Truncate, FileAccess.Write, FileShare.None))
                {
                    writeOutput(output);
                    output.Flush(flushToDisk: true);
                }

                EnsureWritableFile(path);
                // Require write permission even though replacement itself may only
                // require directory permission. On Windows, deny in-place writers
                // while checking, but permit the delete access needed by Replace.
                using (var current = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete))
                {
                    if (!Matches(current, expectedBytes))
                    {
                        throw new IOException($"'{path}' changed during formatting. Run the formatter again.");
                    }

                    try
                    {
                        File.Replace(temporary, path, backup);
                    }
                    catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                    {
                        // Some file systems can fail partway through replacement.
                        // Keep any backup (and staged output) for manual recovery.
                        keepRecoveryFiles = File.Exists(backup) || !File.Exists(path);
                        if (keepRecoveryFiles)
                        {
                            throw new IOException($"Could not replace '{path}'. Recovery files: '{backup}' (original, if present), '{temporary}' (formatted, if present).", exception);
                        }

                        throw;
                    }
                }

                File.Delete(backup);
            }
            finally
            {
                if (!keepRecoveryFiles)
                {
                    File.Delete(temporary);
                }
            }
        }

        private static void EnsureWritableFile(string path)
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Cannot safely replace symbolic link or reparse point '{path}'. Format its target directly.");
            }

            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                throw new UnauthorizedAccessException($"Cannot format read-only file '{path}'.");
            }
        }

        private static bool Matches(Stream current, byte[] expected)
        {
            if (current.Length != expected.Length)
            {
                return false;
            }

            var buffer = new byte[8192];
            var offset = 0;
            int count;
            while ((count = current.Read(buffer, 0, buffer.Length)) != 0)
            {
                for (var index = 0; index < count; index++)
                {
                    if (offset >= expected.Length || buffer[index] != expected[offset++])
                    {
                        return false;
                    }
                }
            }

            return offset == expected.Length;
        }
    }
}
