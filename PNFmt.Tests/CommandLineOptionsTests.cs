using PNFmt.Cli;

using Xunit;

namespace PNFmt.Tests
{
    public sealed class CommandLineOptionsTests
    {
        [Fact]
        public void Informational_options_do_not_hide_errors_in_earlier_arguments()
        {
            var error = Assert.Throws<CommandLineException>(() => CommandLineOptions.Parse(new[] { "-m:0", "--help" }));
            Assert.Equal("Option '-m:0' requires a positive integer after ':'.", error.Message);
        }

        [Theory]
        [InlineData("-h", true)]
        [InlineData("--help", true)]
        [InlineData("/?", true)]
        [InlineData("-V", false)]
        [InlineData("--version", false)]
        public void Informational_options_return_prior_values_without_validating_or_reading_later_arguments(string option, bool help)
        {
            var options = CommandLineOptions.Parse(new[] { "--check", "--write-default-config", "before", option, "--unknown" });

            Assert.Equal(help, options.ShowHelp);
            Assert.Equal(!help, options.ShowVersion);
            Assert.True(options.Check);
            Assert.True(options.DryRun);
            Assert.True(options.WriteDefaultConfig);
            Assert.Equal(new[] { "before" }, options.Paths);
        }

        [Theory]
        [InlineData("--CHECK")]
        [InlineData("--Recursive")]
        [InlineData("--HELP")]
        public void Switch_names_remain_case_sensitive(string option)
        {
            var error = Assert.Throws<CommandLineException>(() => CommandLineOptions.Parse(new[] { option }));
            Assert.Equal($"Unknown option: {option}", error.Message);
        }

        [Fact]
        public void Terminator_preserves_every_remaining_argument_as_a_path()
        {
            var options = CommandLineOptions.Parse(new[] { "--formatter", "csharp", "--", "--help", "--lint", "--", "-m:0" });

            Assert.Equal(new[] { "--help", "--lint", "--", "-m:0" }, options.Paths);
            Assert.Equal(new[] { "csharp" }, options.FormatterNames);
            Assert.False(options.ShowHelp);
            Assert.False(options.Lint);
            Assert.Null(options.MaxCpuCount);
        }

        [Theory]
        [InlineData("--formatter", "formatter")]
        [InlineData("--file-pattern", "file pattern")]
        [InlineData("--migrate-legacy-config", "value of true or false")]
        public void Value_options_reject_blank_following_values(string option, string description)
        {
            var error = Assert.Throws<CommandLineException>(() => CommandLineOptions.Parse(new[] { option, " " }));
            Assert.Equal($"Option '{option}' requires a {description}.", error.Message);
        }

        [Theory]
        [InlineData("--formatter=", "formatter")]
        [InlineData("--formatters:", "formatter")]
        [InlineData("--file-pattern=", "file pattern")]
        [InlineData("--filepattern:", "file pattern")]
        [InlineData("--migrate-legacy-config=", "value of true or false")]
        [InlineData("--remove-legacy-config:", "value of true or false")]
        public void Value_options_reject_empty_attached_values(string option, string description)
        {
            var error = Assert.Throws<CommandLineException>(() => CommandLineOptions.Parse(new[] { option }));
            Assert.Equal($"Option '{option}' requires a {description}.", error.Message);
        }

        [Fact]
        public void Value_options_remain_case_insensitive_and_accumulate_in_order()
        {
            var options = CommandLineOptions.Parse(new[]
            {
                "--FORMATTER=CSharp,resx", "--formatters", "ini", "--FILEPATTERN:*.cs",
                "--file-pattern", "*.resx", "-M:2", "-maxCPUCount:3", "-n", "--lint", "file.cs",
            });

            Assert.Equal(new[] { "CSharp", "resx", "ini" }, options.FormatterNames);
            Assert.Equal(new[] { "*.cs", "*.resx" }, options.FilePatterns);
            Assert.Equal(3, options.MaxCpuCount);
            Assert.True(options.DryRun);
            Assert.True(options.Check);
            Assert.True(options.Lint);
            Assert.Equal(new[] { "file.cs" }, options.Paths);
        }
    }
}
