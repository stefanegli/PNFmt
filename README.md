# PNFmt

> [!WARNING]
> PNFmt is currently alpha software and has not yet been tested for production use. Use it with source control and review the changes it makes.

PNFmt is a .NET global tool for consistently formatting project and supporting files that are not handled by a C# formatter. Formatting behavior is configured through `.editorconfig`, while repository-wide tool settings live in an optional `.pnfmt` file.

Every formatter is opt-in. PNFmt skips a file unless its applicable EditorConfig settings enable formatting.

## Supported files

| Files | Formatter | What it does |
| --- | --- | --- |
| [`.csproj`](docs/formatters/csproj.md) | Project | Formats MSBuild project XML, sorts safe properties and items, and reports project-structure diagnostics. |
| [`.editorconfig`, `.ini`](docs/formatters/ini.md) | Configuration | Sorts properties and sections, groups keys by prefix, and merges duplicate sections. |
| [`.resx`](docs/formatters/resx.md) | Resource | Sorts resource entries and optionally removes generated schema and documentation content. |
| [`.rsp`](docs/formatters/rsp.md) | Response | Sorts lines in .NET compiler response files while respecting comment barriers. |
| [`.slnx`](docs/formatters/slnx.md) | Solution | Orders known solution elements and normalizes XML layout. |

The linked pages describe each formatter's settings, behavior, and order-sensitivity considerations. PNFmt does not format C# source files.

## Installation

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), then install PNFmt from NuGet.org:

```powershell
dotnet tool install --global PetchNaka.PNFmt.Cli --version 0.1.0-alpha.5
```

Update an existing installation with:

```powershell
dotnet tool update --global PetchNaka.PNFmt.Cli --version 0.1.0-alpha.5
```

Verify the installation:

```powershell
pnfmt --version
```

## Usage

```text
pnfmt [options] [<path> ...]
```

With no path, PNFmt processes the current directory. Directory processing is non-recursive unless `--recursive` is passed.

Inside a Git working tree, PNFmt processes only staged, unstaged, and untracked files in the requested scope by default. Pass `--all` to include unchanged files. Outside Git repositories, all supported files in scope are processed.

```powershell
# Format changed supported files in the current directory
pnfmt

# Format changed supported files throughout the directory tree
pnfmt --recursive .

# Format all supported files, including unchanged files
pnfmt --all --recursive .

# Check formatting without changing files
pnfmt --check --recursive .

# Preview changes without returning a failing check result
pnfmt --dry-run --recursive .

# Lint project files and check their formatting
pnfmt --lint --recursive .

# Format only test project files
pnfmt --recursive --file-pattern "**/*Tests.csproj" .

# Run only selected formatters
pnfmt --recursive --formatter csproj,resx .
```

### Options

| Option | Description |
| --- | --- |
| `-a`, `--all` | Process all files in scope instead of only Git changes. |
| `-r`, `--recursive` | Process directory targets recursively. |
| `-v`, `--verbose` | Show per-file statuses and detailed errors. |
| `-m[:N]`, `-maxCpuCount[:N]` | Process up to `N` files concurrently. Without `N`, use the processor count. |
| `--file-pattern <glob>` | Include files matching the glob. Repeat the option to add patterns. |
| `--formatter <name>[,<name>...]` | Run only the named formatters: `csproj`, `ini`, `resx`, `rsp`, or `slnx`. |
| `-n`, `--dry-run` | Preview changes without writing files and return exit code `0`. |
| `--check` | Preview changes without writing files and return exit code `1` when changes are needed. |
| `--lint` | Check project formatting and report project diagnostics without writing files. |
| `--write-default-config` | Add missing all-enabled settings to `.editorconfig` and create `.pnfmt` when missing. |
| `--migrate-legacy-config <true\|false>` | Import legacy formatter settings using current PNFmt names. |
| `--remove-legacy-config <true\|false>` | Remove legacy formatter settings after optional migration. |
| `-h`, `--help` | Show help. |
| `-V`, `--version` | Show version information. |

File patterns support `*` for characters within one path segment, `?` for one character, and `**` for any number of directories.

The command returns exit code `0` on success, `1` when `--check` finds changes or `--lint` finds changes or diagnostics, and `2` for usage, path, or formatting errors.

## Configuration

Run the following command to add defaults that enable every formatter and optional cleanup behavior:

```powershell
pnfmt --write-default-config .
```

The command reuses matching sections in an existing `.editorconfig`, preserves existing values and line order, and inserts only missing settings at their sorted positions. Missing sections are appended and no comments are added. A new `.editorconfig` also gets `root = true`.

If legacy `csproj_formatter_*` or `resx_formatter_*` settings are found, PNFmt asks separately whether to migrate them and whether to remove the old settings. Both answers can be supplied for non-interactive use:

```powershell
pnfmt --write-default-config --migrate-legacy-config=true --remove-legacy-config=true .
```

PNFmt continues to accept legacy setting names as fallbacks and reports warning `PNFMT001` when one is used or ignored. A matching `pnfmt_*` setting takes precedence.

### Repository settings

PNFmt reads optional tool settings from `.pnfmt` at the Git repository root. Command-line options take precedence. The currently supported setting controls the maximum number of files processed concurrently:

```json
{
  "maxCpuCount": 4
}
```

`maxCpuCount` must be a positive integer and defaults to `1`. File-formatting settings remain in `.editorconfig`.

`--write-default-config` creates `.pnfmt` with the suggested value `4` when it does not exist. An existing file is never modified. Inside a Git working tree, the file is created at the repository root; otherwise it is created beside `.editorconfig`.

## Contributing

Please use the [issue tracker](https://github.com/stefanegli/PNFmt/issues) for bug reports and feature requests.

## License

[MIT License](LICENSE)

### Third-party licenses

| Library | License |
| --- | --- |
| [EditorConfig .NET Core](https://github.com/editorconfig/editorconfig-core-net) | [MIT License](https://github.com/editorconfig/editorconfig-core-net/blob/master/LICENSE) |
| [LibGit2Sharp](https://github.com/libgit2/libgit2sharp) | [MIT License](https://github.com/libgit2/libgit2sharp/blob/master/LICENSE.md) |
| [Microsoft.NET.Test.Sdk](https://github.com/microsoft/vstest) | [MIT License](https://github.com/microsoft/vstest/blob/main/LICENSE) |
| [xUnit](https://github.com/xunit/xunit) | [Apache License 2.0 / MIT License](https://github.com/xunit/xunit/blob/main/LICENSE) |
| [NFluent](https://github.com/tpierrain/NFluent) | [Apache License 2.0](https://github.com/nfluent/nfluent/blob/master/LICENSE) |
