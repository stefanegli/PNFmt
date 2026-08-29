# PNFmt

`pnfmt` formats .NET project, solution, resource, and configuration files.

The built-in formatters are:

- `.csproj`, using `CsProjFormatter`
- `.editorconfig` and `.ini`, using `IniFormatter`
- `.resx`, using `ResxFormatter`
- `.slnx`, using `SlnxFormatter`

All formatter code lives in `PNFmt.Core/Formatter` and uses the `PNFmt` namespace. Each formatter declares one or more file extensions. The registry maps every extension to its formatter, so adding another formatter does not change file discovery or dispatch.

## Usage

```text
pnfmt [options] [<path> ...]
```

With no path, `pnfmt` processes the current directory. Directories are non-recursive unless `--recursive` is supplied.

```powershell
dotnet run --project PNFmt.Cli\PNFmt.Cli.csproj -- --check --recursive .
```

Options:

- `-r`, `--recursive`: recurse into directory targets
- `-v`, `--verbose`: show detailed formatter logging
- `-m[:N]`, `-maxCpuCount[:N]`: process up to `N` files concurrently; omit `N` to use the processor count
- `-n`, `--dry-run`: preview without writing and return success
- `--check`: preview and return `1` when a file would change
- `--lint`: run project diagnostics and return `1` for changes or diagnostics
- `-h`, `--help`: show help
- `-V`, `--version`: show version

PNFmt follows MSBuild's parallelism behavior. Without `-m`, it processes one file at a time. `-m` uses the processor count, while `-m:4` or `-maxCpuCount:4` limits processing to four concurrent files.

The normal output is one summary line with the processed and affected file counts plus elapsed time. Use `--verbose` for per-file statuses. PNFmt formats discovered `.editorconfig` files before processing their dependent files in parallel.

Project and resource formatting remain opt-in through EditorConfig:

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

`pnfmt_csproj_sort_item_types` replaces the built-in project item list when set. Omit it to use the defaults. The supported resource comparers are `InvariantCulture`, `InvariantCultureIgnoreCase`, `OrdinalIgnoreCase`, and `Ordinal`.

Settings shared by multiple formatters use the `pnfmt_` prefix directly. Settings that only make sense for one format use `pnfmt_csproj_` or `pnfmt_resx_`.

### Legacy setting compatibility

PNFmt reads the old tool settings as fallbacks:

| PNFmt setting | Legacy fallback |
| --- | --- |
| `pnfmt_sort_entries` | `csproj_formatter_sort_entries` for `.csproj` files |
| `pnfmt_sort_entries` | `resx_formatter_sort_entries` for `.resx` files |
| `pnfmt_csproj_empty_lines_between_groups` | `csproj_formatter_empty_lines_between_groups` |
| `pnfmt_csproj_sort_item_types` | `csproj_formatter_sort_item_types` |
| `pnfmt_resx_remove_xsd_schema` | `resx_formatter_remove_xsd_schema` |
| `pnfmt_resx_remove_documentation_comment` | `resx_formatter_remove_documentation_comment` |
| `pnfmt_resx_sort_comparer` | `resx_formatter_sort_comparer` |

When a legacy setting applies, PNFmt writes warning `PNFMT001` to standard error. If both names apply, the `pnfmt_` setting wins and the warning states that PNFmt ignored the legacy value. These warnings do not change the command's exit code. Standard EditorConfig layout settings such as `indent_style`, `indent_size`, `tab_width`, and `end_of_line` keep their standard names.

SLNX formatting orders the schema-defined solution, folder, project, configuration, and property elements, while leaving unknown extension elements in place as ordering barriers. It uses two-space XML indentation and preserves the file's newline style.

EditorConfig and INI formatting preserves section order because later EditorConfig sections can override earlier ones. Within each uninterrupted property block it sorts keys case-insensitively, normalizes assignments to `key = value`, collapses repeated blank lines, and preserves comments or unknown lines as ordering barriers. Inline `#` and `;` characters remain part of a value.

The command returns `0` on success, `1` when `--check` finds pending changes or `--lint` finds project diagnostics, and `2` for usage, path, or formatting failures.

## Formatter interface

Formatters implement `IFileFormatter` in `PNFmt.Core`. A formatter supplies its name, its `FileExtensions` collection, and a method that formats one file. `FormatterRegistry` validates registrations and performs case-insensitive extension lookup.

## Snapshot tests

`PNFmt.Tests` contains the complete formatter snapshot corpus from both legacy CLI projects:

- 13 CsProjFormatter inputs under `Formatter/CsProj/_files/input`
- 13 ResxFormatter inputs under `Formatter/Resx/_files`
- 8 ResxFormatter EditorConfig scenarios under `Formatter/Resx/_editor`
- 5 INI and EditorConfig inputs under `Formatter/Ini/_files/input`
- 5 SLNX inputs under `Formatter/Slnx/_files/input`

Tests format disposable input copies, then write their current output under `Snapshots/PNFmt.Tests`. The snapshot path mirrors the test namespace, class, method, and case. The CsProj, INI, EditorConfig, and SLNX snapshots also run through the built `pnfmt` process.

Snapshot text uses LF line endings. Comparison normalizes CRLF, LF, and lone CR input before checking Git, so snapshots do not change between Windows and Unix test runs.

Snapshots use Git staging as approval. A test compares its output with the staged snapshot when that snapshot has index changes; otherwise it compares with the version in `HEAD`. The test always overwrites the working-tree snapshot with the current output. A difference fails with a Git patch. Review the generated file, stage it with `git add`, and rerun the test to approve it. Unstaged manual edits never count as approval.

## License

MIT. See [LICENSE](LICENSE).
