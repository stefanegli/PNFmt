---
name: pnfmt
description: Check, format, lint, or configure C#, SDK-style and non-SDK MSBuild, resource, solution, XML, XAML, response, INI, and EditorConfig files with PNFmt. Use only when explicitly invoked.
---

# PNFmt

PNFmt checks, formats, and lints files using EditorConfig settings. Formatter selection can enable files with other extensions, including MSBuild `.props`, `.targets`, and `.proj` files.

This skill's usage and configuration references are bundled in this folder.

## Invocation

The installed CLI is `pnfmt`:

```powershell
pnfmt --help
pnfmt --version
```

`--help` lists the installed version's options; `--version` reports its version.

## Scope and operations

With no path, PNFmt uses the current directory. Directory scope is non-recursive unless `--recursive` is passed. Inside Git, each target uses its own repository and only staged, unstaged, and untracked files in scope are processed by default. `--all` includes unchanged files. Outside Git, all supported files in scope are processed.

- Check without writing: `pnfmt --check <paths>`.
- Preview without failing for differences: `pnfmt --dry-run <paths>`.
- Format files: `pnfmt <paths>`.
- Check formatting and formatter diagnostics without writing: `pnfmt --lint <paths>`.
- Add missing default configuration: `pnfmt --write-default-config <directory-or-editorconfig>`.

Useful narrowing and execution options:

- `--file-pattern <glob>`: repeat for multiple patterns. `*` and `?` stay within one path segment; `**` crosses directories.
- `--formatter <name>[,<name>...]`: filter the resolved `csharp`, `csproj`, `ini`, `resx`, `rsp`, `slnx`, `xml`, or `xaml` formatters. It does not override selection or enablement.
- `-m:N` or `-maxCpuCount:N`: override concurrency. Plain `-m` uses the processor count.
- `--verbose`: show per-file statuses and detailed errors. `--help` lists the current CLI options.

Exit `0` means success; `1` means `--check` found changes or `--lint` found changes or diagnostics; `2` means a usage, path, or formatting error. A skipped file with a diagnostic does not by itself fail ordinary formatting or `--check`.

## Configuration and formatter guidance

Configure `pnfmt_enabled = true` and `pnfmt_formatter = <name>` in the matching section. Formatter names are case-insensitive and independent of the extension. `pnfmt_enabled = false` skips all processing, including linting and encoding changes. `None` remains a compatibility selection for disabling processing; unknown or empty names are errors when processing is enabled.

Layout defaults on (`pnfmt_format = true`); optional sorting and cleanup switches default off. Explicitly enabled project files keep sorting off when omitted, even when the extension supplies a missing formatter selection. Setting `pnfmt_format = false` allows independently enabled transformations and explicit encoding changes to proceed. C# wrapping, Microsoft blank-line preferences, headers, and XML/MSBuild attribute wrapping, arrangement, tag spacing, and quote style require layout. The older `pnfmt_csharp_format`, `pnfmt_xml_format`, and `pnfmt_xaml_format` switches are layout fallbacks; `pnfmt_format` takes precedence.

Normal inheritance applies independently to each setting. `unset` removes an inherited value. Missing activation controls retain compatibility behavior with warning `PNFMT004`. Legacy `csproj_formatter_*` and `resx_formatter_*` names remain fallback aliases with warning `PNFMT001`; current names take precedence. The bundled [configuration reference](references/configuration.md) covers activation, precedence, and legacy migration.

Read only the guidance relevant to the selected formatter:

- [C# reference](references/csharp.md): Roslyn layout, line width, parameter/argument chopping, short-call joining, opt-in initializer/collection/pattern/ternary/query/switch/tuple wrapping, import/modifier/member sorting, region removal, both blank-line mechanisms, file headers, exclusions, generated files, and parse diagnostics.
- [Other formatters](references/other-formatters.md): XML/MSBuild attribute wrapping, tag spacing, and quote escaping, MSBuild sorting/linting, RESX insertion/removal, INI and EditorConfig precedence, response files, solutions, and XML/XAML preservation.

## Encoding and final newlines

All enabled formatters honor `charset`: `utf-8` (no BOM), `utf-8-bom`, `utf-16le`, `utf-16be` (both with BOM), or `latin1` (no BOM). Values are case-insensitive. Missing, invalid, or `unset` values retain formatter-specific encoding behavior. Charset alone never activates processing; it can change encoding with layout disabled, and preview modes detect those changes without writing. For XML-based files, explicit charset updates the encoding declaration, adding one when required for non-UTF-8 output. Encoding errors leave the input untouched rather than replacing characters.

Input BOMs and XML declarations identify the original encoding. BOM-less XML without an encoding declaration is read as UTF-8. Other BOM-less text is read as UTF-8, or as Latin-1 when `charset = latin1`.

With layout enabled, `insert_final_newline` differs by formatter:

| Formatter | `true` | `false` or missing |
| --- | --- | --- |
| C#, XML, XAML | Add a missing final newline. | Preserve whether one existed. |
| Project | End with one newline. | `false` removes it; missing ends with one. |
| Resource | End with one newline. | Omit the final newline. |
| INI, response, solution | End with one newline. | End with one newline. |

Protected content can prevent whitespace changes. RESX serialization protects runtime string newlines with character references, independently of XML layout; newline-bearing CDATA becomes escaped text. See the [resource guidance](references/other-formatters.md#resources-resx). C#, project, resource, XML, and XAML honor explicit layout settings; INI, response, and solution use the detected line ending instead. Solution indentation is fixed at two spaces.

## Repository and generated configuration

The optional `.pnfmt` JSON file supports `maxCpuCount`, a positive integer defaulting to `1`. Each target reads its Git root's configuration, or its target directory's configuration outside Git. If multiple configurations apply, the lowest limit governs the run; command-line concurrency wins. File-formatting policy belongs in `.editorconfig`.

`--write-default-config` enables every formatter and writes explicit behavior choices. It reuses matching sections, preserves existing values and line order, inserts missing keys at sorted positions, appends missing sections, and adds no comments. A new file gets `root = true`; earlier marked PNFmt default blocks are migrated. Existing disabled settings remain disabled.

Generated defaults enable sorting except for response files, C# modifier/member sorting and declaration blank-line cleanup, prefix grouping for `.ini` files, and RESX schema/documentation removal. They disable response-file sorting to preserve compiler argument order, C# region removal, INI/EditorConfig section sorting and merging, and RESX insertion. They preserve existing explicit sorting preferences. They do not add C# width/list styles, XML/MSBuild wrapping/attribute styles, tag spacing, and quote style, Microsoft blank-line preferences, or header templates. These explicit generated choices differ from the defaults for omitted settings above.

The command creates `.pnfmt` with `maxCpuCount` set to `4` only when missing: at the Git root, or beside `.editorconfig` outside Git. It never modifies an existing `.pnfmt`.

Existing `.editorconfig` input is decoded strictly. Updates stage UTF-8 without a BOM and reject intervening byte changes, read-only files, symbolic links, and reparse points rather than overwriting them. Missing files use exclusive creation. Replacement preserves Windows security metadata and Unix permission bits, but changes file identity (other hard links retain old contents); ownership, extended metadata, durability, and a race after the final edit check depend on the file system. Interrupted replacement can leave `.pnfmt-*.tmp` or `.tmp.bak` recovery siblings.

When legacy settings exist, supply both migration choices for non-interactive runs:

```powershell
pnfmt --write-default-config --migrate-legacy-config=<true|false> --remove-legacy-config=<true|false> <directory-or-editorconfig>
```

Migration adds current names with legacy values in the same sections. Removal independently deletes legacy names when `--remove-legacy-config=true`.
