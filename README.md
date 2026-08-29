# PNFmt

`pnfmt` formats .NET project and resource files according to EditorConfig.

The initial providers are:

- `.csproj`, using the CsProjFormatter implementation
- `.resx`, using the ResxFormatter implementation

Each provider declares one or more file extensions. The registry maps every declared extension to its provider, so adding another extension does not change file discovery or dispatch.

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

Formatting remains opt-in through the existing settings:

```ini
[*.csproj]
csproj_formatter_sort_entries = true

[*.resx]
resx_formatter_sort_entries = true
resx_formatter_remove_xsd_schema = true
resx_formatter_remove_documentation_comment = true
```

## Provider interface

Providers implement `IFileFormatterProvider` in `PNFmt.Core`. A provider supplies its name, its `FileExtensions` collection, and a method that formats one file. `FormatterProviderRegistry` validates the registrations and performs case-insensitive extension lookup.

## License

MIT. See [LICENSE](LICENSE).

