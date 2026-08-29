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
- `-n`, `--dry-run`: preview without writing and return success
- `--check`: preview and return `1` when a file would change
- `--lint`: run project diagnostics and return `1` for changes or diagnostics
- `-h`, `--help`: show help
- `-V`, `--version`: show version

Project and resource formatting remain opt-in through the existing settings:

```ini
[*.csproj]
csproj_formatter_sort_entries = true
csproj_formatter_empty_lines_between_groups = 1
indent_style = space
tab_width = 4
end_of_line = crlf

[*.resx]
resx_formatter_sort_entries = true
resx_formatter_remove_xsd_schema = true
resx_formatter_remove_documentation_comment = true
resx_formatter_sort_comparer = OrdinalIgnoreCase
```

`csproj_formatter_sort_item_types` replaces the built-in list when set. Omit it to use the defaults. The supported resource comparers are `InvariantCulture`, `InvariantCultureIgnoreCase`, `OrdinalIgnoreCase`, and `Ordinal`.

SLNX formatting orders the schema-defined solution, folder, project, configuration, and property elements, while leaving unknown extension elements in place as ordering barriers. It uses two-space XML indentation and preserves the file's newline style.

EditorConfig and INI formatting preserves section order because later EditorConfig sections can override earlier ones. Within each uninterrupted property block it sorts keys case-insensitively, normalizes assignments to `key = value`, collapses repeated blank lines, and preserves comments or unknown lines as ordering barriers. Inline `#` and `;` characters remain part of a value.

The command returns `0` on success, `1` when `--check` finds pending changes or `--lint` finds project diagnostics, and `2` for usage, path, or formatting failures.

## Formatter interface

Formatters implement `IFileFormatter` in `PNFmt.Core`. A formatter supplies its name, its `FileExtensions` collection, and a method that formats one file. `FormatterRegistry` validates registrations and performs case-insensitive extension lookup.

## License

MIT. See [LICENSE](LICENSE).
