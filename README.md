# PNFmt

> [!WARNING]
> PNFmt is currently alpha software and has not yet been tested for production use. Use it with source control and review the changes it makes.

`pnfmt` is a .NET global tool that formats these files:

- `.csproj` project files
- `.editorconfig` and `.ini` configuration files
- `.resx` resource files
- `.slnx` solution files

Project and resource formatting follows the settings in the applicable `.editorconfig` file. EditorConfig, INI, and SLNX formatting works without formatter-specific configuration.

## Installation

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), then install PNFmt from NuGet.org:

```powershell
dotnet tool install --global PetchNaka.PNFmt.Cli --version 0.1.0-alpha
```

Update an existing installation with:

```powershell
dotnet tool update --global PetchNaka.PNFmt.Cli --version 0.1.0-alpha
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

Add the settings you want to an `.editorconfig` file. At least one supported PNFmt setting must apply before PNFmt formats a `.csproj` or `.resx` file.

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
