# PNFmt

> [!WARNING]
> PNFmt is currently alpha software and has not yet been tested for production use. Use it with source control and review the changes it makes.

PNFmt is a .NET global tool for consistently formatting C# source, project, and supporting files. It processes each file independently without loading a project or solution. Formatting behavior is configured through `.editorconfig`, while repository-wide tool settings live in an optional `.pnfmt` file.

Enable processing with `pnfmt_enabled = true` and choose a formatter with `pnfmt_formatter = <name>` in the applicable `.editorconfig` section. `pnfmt_enabled = false` disables processing while retaining the selection and options. Layout defaults on (`pnfmt_format = true`); optional sorting and cleanup default off unless configured. For this version, missing enablement or selection retains compatibility behavior with warning `PNFMT004`. See [activation, behavior switches, and final-newline rules](docs/configuration-contracts.md).

## Supported files

| Files | Formatter | Status | What it does |
| --- | --- | --- | --- |
| [`.cs`](docs/formatters/csharp.md) | C# | Experimental | Formats whitespace and wraps code, with parameter/argument list styles, import/modifier/member sorting, blank-line preferences, file headers, region removal, and exclusion regions. Member ordering preserves storage declaration order. |
| [`.csproj`](docs/formatters/csproj.md) | Project | Stable | Formats MSBuild project XML, optionally wraps attributes, sorts safe properties and items, and reports project-structure diagnostics. |
| [`.editorconfig`, `.ini`](docs/formatters/ini.md) | Configuration | Experimental | Sorts properties and sections, groups keys by prefix, and merges duplicate sections. |
| [`.resx`](docs/formatters/resx.md) | Resource | Stable | Sorts resource entries and optionally removes generated schema and documentation content. |
| [`.rsp`](docs/formatters/rsp.md) | Response | Experimental | Sorts lines in .NET compiler response files while respecting comment barriers. |
| [`.slnx`](docs/formatters/slnx.md) | Solution | Experimental | Orders known solution elements and normalizes XML layout. |
| [`.xml`](docs/formatters/xml-xaml.md) | XML | Experimental | Indents structural markup and optionally wraps attributes while preserving values and protected text subtrees. |
| [`.xaml`](docs/formatters/xml-xaml.md) | XAML | Experimental | Indents known layout/resource containers and optionally wraps attributes while protecting inline text and custom-container content whitespace. |

Status reflects each formatter's maturity; PNFmt as a whole remains alpha software.

The linked pages describe each formatter's settings, behavior, and order-sensitivity considerations.

## Installation

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), then install PNFmt from NuGet.org:

```powershell
dotnet tool install --global PetchNaka.PNFmt.Cli --version 0.1.0-alpha.10
```

Update an existing installation with:

```powershell
dotnet tool update --global PetchNaka.PNFmt.Cli --version 0.1.0-alpha.10
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

For targets inside a Git working tree, PNFmt processes only staged, unstaged, and untracked files in the requested scope by default, regardless of the caller's working directory. Each target uses its own repository. Pass `--all` to include unchanged files. Outside Git repositories, all supported files in scope are processed.

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
| `--formatter <name>[,<name>...]` | Run only the named formatters: `csharp`, `csproj`, `ini`, `resx`, `rsp`, `slnx`, `xml`, or `xaml`. |
| `-n`, `--dry-run` | Preview changes without writing files and return exit code `0`. |
| `--check` | Preview changes without writing files and return exit code `1` when changes are needed. |
| `--lint` | Check formatting and report formatter diagnostics without writing files. |
| `--write-default-config` | Add missing formatter defaults to `.editorconfig` and create `.pnfmt` when missing. |
| `--migrate-legacy-config <true\|false>` | Import legacy formatter settings using current PNFmt names. |
| `--remove-legacy-config <true\|false>` | Remove legacy formatter settings after optional migration. |
| `-h`, `--help` | Show help. |
| `-V`, `--version` | Show version information. |

File patterns support `*` for characters within one path segment, `?` for one character, and `**` for any number of directories.

The command returns exit code `0` on success, `1` when `--check` finds changes or `--lint` finds changes or diagnostics, and `2` for usage, path, or formatting errors.

## Configuration

Run the following command to add defaults that enable every formatter with sorting and selected cleanup behaviors. C# region removal and INI/EditorConfig section sorting and merging remain off. C# and XML/MSBuild wrapping, attribute styles, Microsoft blank-line preferences, and file headers are not configured:

```powershell
pnfmt --write-default-config .
```

The command reuses matching sections in an existing `.editorconfig`, preserves existing values and line order, and inserts only missing settings at their sorted positions. Missing sections are appended and no comments are added. A new `.editorconfig` also gets `root = true`.

If legacy `csproj_formatter_*` or `resx_formatter_*` settings are found, PNFmt asks separately whether to migrate them and whether to remove the old settings. Both answers can be supplied for non-interactive use:

```powershell
pnfmt --write-default-config --migrate-legacy-config=true --remove-legacy-config=true .
```

PNFmt continues to accept legacy setting names as fallbacks and reports warning `PNFMT001` when one is used or ignored. A matching `pnfmt_*` setting takes precedence.

### File encoding

All enabled formatters honor EditorConfig's `charset` setting, including changes that affect only encoding or the BOM:

| `charset` | Output encoding |
| --- | --- |
| `utf-8` | UTF-8 without a BOM. |
| `utf-8-bom` | UTF-8 with a BOM. |
| `utf-16le` | UTF-16 little-endian with a BOM. |
| `utf-16be` | UTF-16 big-endian with a BOM. |
| `latin1` | ISO-8859-1 without a BOM. |

Values are case-insensitive and follow normal EditorConfig inheritance. `charset` alone does not enable a formatter. Missing, invalid, or `unset` values retain each formatter's existing encoding behavior. `--check` detects encoding changes and `--dry-run` previews them without writing.

For XML-based files, an explicit charset updates the XML declaration to match the output encoding, adding a declaration when required for non-UTF-8 output. Input BOMs and XML declarations identify the original encoding. BOM-less XML without an encoding declaration is read as UTF-8. Other BOM-less text is read as UTF-8, or as Latin-1 when `charset = latin1`. Encoding failures leave the original file untouched; characters are never silently replaced.

### Repository settings

PNFmt reads optional tool settings from `.pnfmt` at each target's Git repository root, or in the target directory for paths outside Git. If the command spans several configurations, the lowest concurrency limit applies to the entire run. Command-line options take precedence. The currently supported setting controls the maximum number of files processed concurrently:

```json
{
  "maxCpuCount": 4
}
```

`maxCpuCount` must be a positive integer and defaults to `1`. File-formatting settings remain in `.editorconfig`.

`--write-default-config` creates `.pnfmt` with the suggested value `4` when it does not exist. An existing file is never modified. Inside a Git working tree, the file is created at the repository root; otherwise it is created beside `.editorconfig`.

## Contributing

See the [formatter architecture](docs/architecture.md) for module responsibilities and verification.

Run `./scripts/Format-Repository.cmd` on Windows, or `./scripts/Format-Repository.ps1` in PowerShell 7, to format the repository with PNFmt. Add `-Check` to verify without writing. The command includes C# test code but excludes fixture data (`_files`, `_editor`), expected snapshots (`Snapshots/`), and build output. Use this command for repository-wide formatting: fixture `.editorconfig` files deliberately enable transformations and can override exclusions during a direct recursive PNFmt run.

Run `./scripts/Test-Coverage.ps1` in PowerShell to test with coverage and generate an HTML report. See [test coverage](docs/test-coverage.md) for reports, minimum thresholds, and CI enforcement.

Run `./scripts/Test-Performance.ps1` to compare Release performance with the pinned Git baseline on the same machine. The publish script runs this gate before packing or pushing, including `-PackOnly` and invocations with other skip switches. Timing and allocation changes are reported per case; intentional baseline updates require a committed explanation. See [performance gates](docs/performance.md) for limits, reports, and baseline maintenance.

Use the [HTML snapshot viewer](tools/SnapshotViewer/README.md) to inspect test inputs, expected cleanup, and configuration by formatter.

Please use the [issue tracker](https://github.com/stefanegli/PNFmt/issues) for bug reports and feature requests.

## License

[MIT License](LICENSE)

### Third-party licenses

| Library | License |
| --- | --- |
| [EditorConfig .NET Core](https://github.com/editorconfig/editorconfig-core-net) | [MIT License](https://github.com/editorconfig/editorconfig-core-net/blob/master/LICENSE) |
| [LibGit2Sharp](https://github.com/libgit2/libgit2sharp) | [MIT License](https://github.com/libgit2/libgit2sharp/blob/master/LICENSE.md) |
| [Roslyn](https://github.com/dotnet/roslyn) | [MIT License](https://github.com/dotnet/roslyn/blob/main/License.txt) |
| [Microsoft.NET.Test.Sdk](https://github.com/microsoft/vstest) | [MIT License](https://github.com/microsoft/vstest/blob/main/LICENSE) |
| [xUnit](https://github.com/xunit/xunit) | [Apache License 2.0 / MIT License](https://github.com/xunit/xunit/blob/main/LICENSE) |
| [NFluent](https://github.com/tpierrain/NFluent) | [Apache License 2.0](https://github.com/nfluent/nfluent/blob/master/LICENSE) |
