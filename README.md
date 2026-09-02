# PNFmt

> [!WARNING]
> PNFmt is currently alpha software and has not yet been tested for production use. Use it with source control and review the changes it makes.

`pnfmt` is a .NET global tool that formats these files:

- `.csproj` project files
- `.editorconfig` and `.ini` configuration files
- `.resx` resource files
- `.slnx` solution files

Every formatter is opt-in. PNFmt skips a file unless the applicable `.editorconfig` explicitly enables formatting for that file type.

## Installation

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), then install PNFmt from NuGet.org:

```powershell
dotnet tool install --global PetchNaka.PNFmt.Cli --version 0.1.0-alpha.2
```

Update an existing installation with:

```powershell
dotnet tool update --global PetchNaka.PNFmt.Cli --version 0.1.0-alpha.2
```

Verify the installation:

```powershell
pnfmt --version
```

## Usage

Run `pnfmt` from the directory containing the files you want to format:

```text
pnfmt [options] [<path> ...]
```

Without a path, PNFmt processes the current directory. Directory processing is non-recursive unless you pass `--recursive`.

```powershell
# Format supported files in the current directory and its subdirectories
pnfmt --recursive .

# Check formatting without changing files
pnfmt --check --recursive .

# Preview changes without changing files or returning a failing check result
pnfmt --dry-run --recursive .

# Lint project files and check their formatting
pnfmt --lint --recursive .

# Format only test project files
pnfmt --recursive --file-pattern "**/*Tests.csproj" .

# Run only the project and resource formatters
pnfmt --recursive --formatter csproj,resx .
```

### Options

| Option | Description |
| --- | --- |
| `-r`, `--recursive` | Process directory targets recursively. |
| `-v`, `--verbose` | Show per-file statuses and detailed errors. |
| `-m[:N]`, `-maxCpuCount[:N]` | Process up to `N` files at once. Without `N`, use the processor count. |
| `--file-pattern <glob>` | Include files matching the glob. Repeat the option to add patterns. |
| `--formatter <name>[,<name>...]` | Run only the named formatters. Available names are `csproj`, `ini`, `resx`, and `slnx`. |
| `-n`, `--dry-run` | Preview changes without writing files and return exit code `0`. |
| `--check` | Preview changes without writing files and return exit code `1` when changes are needed. |
| `--lint` | Check project formatting and report project diagnostics without writing files. |
| `-h`, `--help` | Show help. |
| `-V`, `--version` | Show the CLI version. |

File patterns support `*` for characters within one path segment, `?` for one character, and `**` for any number of directories. PNFmt processes one file at a time by default. Use `-m`, `-m:4`, or `-maxCpuCount:4` to enable parallel processing.

The command returns exit code `0` on success, `1` when `--check` finds changes or `--lint` finds changes or diagnostics, and `2` for usage, path, or formatting errors.

## Configuration

Add the settings you want to an `.editorconfig` file. Every file type requires explicit configuration. PNFmt reports files for which no supported setting enables a formatter as `skipped`.

```ini
[*.csproj]
pnfmt_sort_entries = true
pnfmt_csproj_empty_lines_between_groups = 1
indent_style = space
tab_width = 4
end_of_line = crlf

[*.resx]
pnfmt_sort_entries = true
pnfmt_resx_remove_xsd_schema = true
pnfmt_resx_remove_documentation_comment = true
pnfmt_resx_sort_comparer = OrdinalIgnoreCase

[*.editorconfig]
pnfmt_sort_entries = true

[*.ini]
pnfmt_sort_entries = true
pnfmt_ini_sort_groups = true

[*.slnx]
pnfmt_sort_entries = true
```

### Project settings

| Setting | Description |
| --- | --- |
| `pnfmt_sort_entries` | Sort eligible properties and items when set to `true`. |
| `pnfmt_csproj_empty_lines_between_groups` | Set the number of empty lines between top-level groups. |
| `pnfmt_csproj_sort_item_types` | Replace the built-in list of sortable item types. Separate names with commas or semicolons, or use `*` for any homogeneous item type. |
| `indent_style`, `indent_size`, `tab_width`, `end_of_line` | Control standard XML layout through EditorConfig. |

Omit `pnfmt_csproj_sort_item_types` to use the built-in item-type list. Sorting keeps evaluation-sensitive project entries in their original order.

### Resource settings

| Setting | Description |
| --- | --- |
| `pnfmt_sort_entries` | Sort resource entries when set to `true`. |
| `pnfmt_resx_remove_xsd_schema` | Remove the embedded XSD schema when set to `true`. |
| `pnfmt_resx_remove_documentation_comment` | Remove the standard documentation comment when set to `true`. |
| `pnfmt_resx_sort_comparer` | Set the entry comparer to `InvariantCulture`, `InvariantCultureIgnoreCase`, `OrdinalIgnoreCase`, or `Ordinal`. |

### Configuration-file settings

| Setting | Description |
| --- | --- |
| `pnfmt_sort_entries` | Sort keys within uninterrupted property blocks. |
| `pnfmt_ini_sort_groups` | Sort named sections by their headers using ordinal, case-insensitive comparison. The preamble stays at the top, and each header moves with everything up to the next header. |

Set at least one of these settings to `true` for each `.editorconfig` or `.ini` pattern that PNFmt should format. The formatter also normalizes assignments to `key = value` and collapses repeated blank lines. It preserves comments and unknown lines.

An `.editorconfig` file with `root = true` must contain its own matching `[*.editorconfig]` section because it does not inherit settings from a parent file.

Section order can affect how tools interpret duplicate INI sections. It also controls precedence between matching sections in `.editorconfig` files. Enable `pnfmt_ini_sort_groups` only when changing that order is safe.

### Solution settings

Set `pnfmt_sort_entries = true` for each `.slnx` pattern that PNFmt should format. The formatter orders known solution elements, uses two-space XML indentation, and preserves unknown extension elements as ordering barriers.

For SLNX files, only `pnfmt_sort_entries = true` enables the formatter. PNFmt skips INI and SLNX files when none of their activation settings are `true`. Missing settings, `false`, and invalid values do not activate a formatter.

PNFmt still accepts the legacy `csproj_formatter_*` and `resx_formatter_*` setting names as fallbacks. It reports warning `PNFMT001` when it uses or ignores a legacy setting. A matching `pnfmt_*` setting takes precedence.

## Contributing

Please use the [issue tracker](https://github.com/stefanegli/PNFmt/issues) for submitting bug reports or feature requests.

## License

[MIT License](LICENSE)

### Third party licenses

| Library | License |
| --- | --- |
| [EditorConfig .NET Core](https://github.com/editorconfig/editorconfig-core-net) | [MIT License](https://github.com/editorconfig/editorconfig-core-net/blob/master/LICENSE) |
| [LibGit2Sharp](https://github.com/libgit2/libgit2sharp) | [MIT License](https://github.com/libgit2/libgit2sharp/blob/master/LICENSE.md) |
| [Microsoft.NET.Test.Sdk](https://github.com/microsoft/vstest) | [MIT License](https://github.com/microsoft/vstest/blob/main/LICENSE) |
| [xUnit](https://github.com/xunit/xunit) | [Apache License 2.0 / MIT License](https://github.com/xunit/xunit/blob/main/LICENSE) |
| [NFluent](https://github.com/tpierrain/NFluent) | [Apache License 2.0](https://github.com/tpierrain/NFluent/blob/master/LICENSE) |
