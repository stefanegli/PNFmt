---
name: pnfmt
description: Check, format, lint, or configure C#, SDK-style and non-SDK MSBuild, resource, solution, XML, XAML, response, INI, and EditorConfig files with PNFmt. Use only when explicitly invoked.
---

# PNFmt

Use PNFmt within the requested scope. Formatter selection through EditorConfig can enable files with other extensions, including MSBuild `.props`, `.targets`, and `.proj` files.

## Invocation

In the PNFmt source repository, replace `pnfmt` in the commands below with:

```powershell
dotnet run --project PNFmt.Cli/PNFmt.Cli.csproj --configuration Release -- <pnfmt-arguments>
```

Elsewhere, use the installed `pnfmt` command and check `pnfmt --version` when feature availability matters. An installed release may predate the checkout. Do not install or update the tool unless requested.

For repository-wide formatting of PNFmt itself, use `scripts/Format-Repository.cmd` from the repository root (or `scripts/Format-Repository.ps1` with PowerShell 7/Linux), then verify with `-Check -NoBuild`. This script excludes fixture inputs and expected snapshots; recursive formatting over the repository root can modify them because fixture EditorConfig files can override exclusions.

## Scope and operations

With no path, PNFmt uses the current directory. Directory scope is non-recursive unless `--recursive` is passed. Inside Git, each target uses its own repository and only staged, unstaged, and untracked files in scope are processed by default. Use `--all` when the task includes unchanged files. Outside Git, all supported files in scope are processed.

- Check without writing: `pnfmt --check <paths>`.
- Preview without failing for differences: `pnfmt --dry-run <paths>`.
- Format when the task permits writes: `pnfmt <paths>`.
- Check formatting and formatter diagnostics without writing: `pnfmt --lint <paths>`.
- Add missing default configuration when requested: `pnfmt --write-default-config <directory-or-editorconfig>`.

Useful narrowing and execution options:

- `--file-pattern <glob>`: repeat for multiple patterns. `*` and `?` stay within one path segment; `**` crosses directories.
- `--formatter <name>[,<name>...]`: filter the resolved `csharp`, `csproj`, `ini`, `resx`, `rsp`, `slnx`, `xml`, or `xaml` formatters. It does not override selection or enablement.
- `-m:N` or `-maxCpuCount:N`: override concurrency. Plain `-m` uses the processor count.
- `--verbose`: show per-file statuses and detailed errors. `--help` lists the current CLI options.

Exit `0` means success; `1` means `--check` found changes or `--lint` found changes or diagnostics; `2` means a usage, path, or formatting error. A skipped file with a diagnostic does not by itself fail ordinary formatting or `--check`. Inspect diagnostics when a file unexpectedly stays unchanged.

## Configuration and formatter guidance

Configure `pnfmt_enabled = true` and `pnfmt_formatter = <name>` in the matching section. Formatter names are case-insensitive and independent of the extension. `pnfmt_enabled = false` skips all processing, including linting and encoding changes. `None` remains a compatibility selection for disabling processing; unknown or empty names are errors when processing is enabled.

Layout defaults on (`pnfmt_format = true`); optional sorting and cleanup switches default off. Setting `pnfmt_format = false` allows independently enabled transformations and explicit encoding changes to proceed. C# wrapping, Microsoft blank-line preferences, and headers require layout. The older `pnfmt_csharp_format`, `pnfmt_xml_format`, and `pnfmt_xaml_format` switches are layout fallbacks; `pnfmt_format` takes precedence.

Normal inheritance applies independently to each setting. `unset` removes an inherited value. Missing activation controls retain compatibility behavior with warning `PNFMT004`; do not enable skipped files unless configuration changes are part of the task. Legacy `csproj_formatter_*` and `resx_formatter_*` names remain fallback aliases with warning `PNFMT001`; current names take precedence. Read the repository's [configuration contracts](../../../docs/configuration-contracts.md) when diagnosing legacy activation or precedence.

Read only the guidance relevant to the selected formatter:

- [C# reference](references/csharp.md): Roslyn layout, line width, parameter/argument chopping, import/modifier/member sorting, region removal, both blank-line mechanisms, file headers, exclusions, generated files, and parse diagnostics.
- [Other formatters](references/other-formatters.md): MSBuild sorting/linting, RESX insertion/removal, INI and EditorConfig precedence, response files, solutions, and XML/XAML preservation.

## Encoding and final newlines

All enabled formatters honor `charset`: `utf-8` (no BOM), `utf-8-bom`, `utf-16le`, `utf-16be` (both with BOM), or `latin1` (no BOM). Values are case-insensitive. Missing, invalid, or `unset` values retain formatter-specific encoding behavior. Charset alone never activates processing; it can change encoding with layout disabled, and preview modes detect those changes without writing. For XML-based files, explicit charset updates the encoding declaration. Encoding errors leave the input untouched rather than replacing characters. See [encoding details](../../../README.md#file-encoding).

With layout enabled, `insert_final_newline` differs by formatter:

| Formatter | `true` | `false` or missing |
| --- | --- | --- |
| C#, XML, XAML | Add a missing final newline. | Preserve whether one existed. |
| Project | End with one newline. | `false` removes it; missing ends with one. |
| Resource | End with one newline. | Omit the final newline. |
| INI, response, solution | End with one newline. | End with one newline. |

Protected content can prevent whitespace changes. C#, project, resource, XML, and XAML honor explicit layout settings; INI, response, and solution use the detected line ending instead. Solution indentation is fixed at two spaces.

## Repository and generated configuration

The optional `.pnfmt` JSON file supports `maxCpuCount`, a positive integer defaulting to `1`. Each target reads its Git root's configuration, or its target directory's configuration outside Git. If multiple configurations apply, the lowest limit governs the run; command-line concurrency wins. File-formatting policy belongs in `.editorconfig`.

`--write-default-config` enables every formatter and writes explicit behavior choices. It reuses matching sections, preserves existing values and line order, inserts missing keys at sorted positions, appends missing sections, and adds no comments. A new file gets `root = true`; earlier marked PNFmt default blocks are migrated. Existing disabled settings remain disabled.

Generated defaults enable sorting where supported, C# modifier/member sorting and declaration blank-line cleanup, prefix grouping for `.ini` files, and RESX schema/documentation removal. They disable C# region removal, INI/EditorConfig section sorting and merging, and RESX insertion. They do not add C# width, list styles, Microsoft blank-line preferences, or header templates. Distinguish these generated choices from defaults for omitted settings.

The command creates `.pnfmt` with `maxCpuCount` set to `4` only when missing: at the Git root, or beside `.editorconfig` outside Git. It never modifies an existing `.pnfmt`.

When legacy settings exist, supply both migration choices for non-interactive runs:

```powershell
pnfmt --write-default-config --migrate-legacy-config=<true|false> --remove-legacy-config=<true|false> <directory-or-editorconfig>
```

Migration adds current names with legacy values in the same sections. Removal is independent; enable it only when deleting legacy names is in scope.

## Completion

After formatting, verify with `--check` on the same paths, recursion, and selection options. Review the diff and report changes and diagnostics. For changes to PNFmt itself, follow `AGENTS.md` for documentation maintenance, commits, and applicable quality gates.
