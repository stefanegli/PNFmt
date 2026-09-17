// Copyright (c) 2026 by Stefan Egli. All rights reserved.

using System;
using System.IO;
using System.Security.AccessControl;
using System.Text;

using Xunit;

namespace PNFmt.Tests.Formatter
{
    public sealed class FileReplacementTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "PNFmtWriteTests-" + Guid.NewGuid().ToString("N"));
        private readonly string path;
        private readonly byte[] original = Encoding.UTF8.GetBytes("original content");

        public FileReplacementTests()
        {
            Directory.CreateDirectory(this.directory);
            this.path = Path.Combine(this.directory, "input.txt");
            File.WriteAllBytes(this.path, this.original);
        }

        [Fact]
        public void Deleted_source_is_not_recreated()
        {
            Assert.Throws<FileNotFoundException>(() => FileReplacement.Write(this.path, this.original, output =>
            {
                output.WriteByte(1);
                File.Delete(this.path);
            }));

            Assert.Empty(Directory.GetFiles(this.directory));
        }

        public void Dispose()
        {
            if (File.Exists(this.path))
            {
                File.SetAttributes(this.path, FileAttributes.Normal);
            }

            Directory.Delete(this.directory, recursive: true);
        }

        [Theory]
        [InlineData("edited content!!")]
        [InlineData("short")]
        [InlineData("longer edited content")]
        public void Edits_during_staging_are_preserved_even_with_the_original_timestamp(string edited)
        {
            var timestamp = File.GetLastWriteTimeUtc(this.path);
            var exception = Assert.Throws<IOException>(() => FileReplacement.Write(this.path, this.original, output =>
            {
                output.WriteByte(1);
                File.WriteAllText(this.path, edited);
                File.SetLastWriteTimeUtc(this.path, timestamp);
            }));

            Assert.Contains("changed during formatting", exception.Message);
            Assert.Equal(edited, File.ReadAllText(this.path));
            this.AssertNoTemporaryFiles();
        }

        [Fact]
        public void Interrupted_output_leaves_original_intact_and_cleans_up()
        {
            var failure = new IOException("Simulated interrupted write");
            var actual = Assert.Throws<IOException>(() => FileReplacement.Write(this.path, this.original, output =>
            {
                output.WriteByte(1);
                output.Flush();
                Assert.Equal(this.original, File.ReadAllBytes(this.path));
                throw failure;
            }));

            Assert.Same(failure, actual);
            Assert.Equal(this.original, File.ReadAllBytes(this.path));
            this.AssertNoTemporaryFiles();
        }

        [Fact]
        public void Pipeline_compares_against_the_bytes_used_for_formatting()
        {
            var request = new FileFormatRequest(this.path, true, false, NullFormatterLog.Instance);
            Assert.Throws<IOException>(() => TextFileFormatPipeline.Format(request, true, text =>
            {
                File.WriteAllText(this.path, "external edit");
                return text.ToUpperInvariant();
            }));

            Assert.Equal("external edit", File.ReadAllText(this.path));
            this.AssertNoTemporaryFiles();
        }

        [Fact]
        public void Preview_does_not_require_write_permission_or_create_temporary_files()
        {
            File.SetAttributes(this.path, File.GetAttributes(this.path) | FileAttributes.ReadOnly);
            var request = new FileFormatRequest(this.path, false, false, NullFormatterLog.Instance);
            Assert.Equal(FileFormatStatus.Updated, TextFileFormatPipeline.Format(request, true, text => text.ToUpperInvariant()).Status);

            Assert.Equal(this.original, File.ReadAllBytes(this.path));
            this.AssertNoTemporaryFiles();
        }

        [Fact]
        public void Read_only_source_is_not_replaced()
        {
            File.SetAttributes(this.path, File.GetAttributes(this.path) | FileAttributes.ReadOnly);
            Assert.Throws<UnauthorizedAccessException>(() => FileReplacement.Write(this.path, this.original, output => output.WriteByte(1)));

            Assert.Equal(this.original, File.ReadAllBytes(this.path));
            this.AssertNoTemporaryFiles();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        [InlineData(20000)]
        public void Replacement_writes_complete_output_and_removes_temporary_files(int length)
        {
            var formatted = Encoding.UTF8.GetBytes(new string('x', length));
            FileReplacement.Write(this.path, this.original, output => output.Write(formatted));

            Assert.Equal(formatted, File.ReadAllBytes(this.path));
            this.AssertNoTemporaryFiles();
        }

        [Fact]
        public void Sharing_failure_does_not_truncate_the_source()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Windows enforces delete sharing; Unix rename does not.
            }

            using (new FileStream(this.path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                Assert.Throws<IOException>(() => FileReplacement.Write(this.path, this.original, output => output.WriteByte(1)));
            }

            Assert.Equal(this.original, File.ReadAllBytes(this.path));
            this.AssertNoTemporaryFiles();
        }

        [Fact]
        public void Symbolic_link_is_not_replaced_or_followed_for_writing()
        {
            var link = Path.Combine(this.directory, "link.txt");
            try
            {
                File.CreateSymbolicLink(link, this.path);
            }
            catch (IOException linkError) when (OperatingSystem.IsWindows() && (linkError.HResult & 0xFFFF) == 1314)
            {
                return; // Some Windows agents do not permit symbolic link creation.
            }

            var exception = Assert.Throws<IOException>(() => FileReplacement.Write(link, this.original, output => output.WriteByte(1)));
            Assert.Contains("Format its target directly", exception.Message);
            Assert.NotNull(new FileInfo(link).LinkTarget);
            Assert.Equal(this.original, File.ReadAllBytes(this.path));
            File.Delete(link);
            this.AssertNoTemporaryFiles();
        }

        [Fact]
        public void Unix_permissions_survive_replacement()
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            var permissions = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead;
            File.SetUnixFileMode(this.path, permissions);
            FileReplacement.Write(this.path, this.original, output => output.WriteByte(1));

            Assert.Equal(permissions, File.GetUnixFileMode(this.path));
            this.AssertNoTemporaryFiles();
        }

        [Fact]
        public void Windows_access_rules_survive_replacement()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            var info = new FileInfo(this.path);
            var security = info.GetAccessControl(AccessControlSections.Access);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: true);
            info.SetAccessControl(security);
            var originalRules = info.GetAccessControl(AccessControlSections.Access).GetSecurityDescriptorSddlForm(AccessControlSections.Access);

            FileReplacement.Write(this.path, this.original, output => output.WriteByte(1));

            Assert.Equal(originalRules, new FileInfo(this.path).GetAccessControl(AccessControlSections.Access).GetSecurityDescriptorSddlForm(AccessControlSections.Access));
            this.AssertNoTemporaryFiles();
        }

        private void AssertNoTemporaryFiles() => Assert.Equal(new[] { this.path }, Directory.GetFiles(this.directory));
    }
}
