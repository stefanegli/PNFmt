---
name: pnfmt
description: Check, format, lint, or configure .NET project, resource, solution, response, INI, and EditorConfig files with PNFmt. Use only when explicitly invoked.
---

# PNFmt

Use PNFmt only within the task's scope. It supports `.csproj`, `.resx`, `.slnx`, `.rsp`, `.ini`, and `.editorconfig` files. It does not format C# source.

## Invocation

In the PNFmt source repository, replace `pnfmt` in the commands below with:

```powershell
dotnet run --project PNFmt.Cli/PNFmt.Cli.csproj --configuration Release -- <pnfmt-arguments>
```

Elsewhere, use an installed `pnfmt` command. Do not install or update the tool unless requested.

## Scope selection

With no path, PNFmt uses the current directory. Directory scope is non-recursive unless `--recursive` is passed.

Inside a Git working tree, PNFmt processes only staged, unstaged, and untracked files in the requested scope. Use `--all` only when the task calls for unchanged files. Outside Git, it processes all supported files in scope.

Useful narrowing and execution options:

- `--recursive`: include subdirectories.
- `--file-pattern <glob>`: include a glob; repeat for multiple patterns. `*` and `?` stay within one path segment, while `**` crosses directories.
- `--formatter <name>[,<name>...]`: select `csproj`, `ini`, `resx`, `rsp`, or `slnx` formatters.
- `-m:N` or `-maxCpuCount:N`: override the configured concurrency. Plain `-m` uses the processor count.
- `--verbose`: show per-file statuses and detailed errors.

## Operations

- Check without writing: `pnfmt --check <paths>`.
- Preview without writing or failing for differences: `pnfmt --dry-run <paths>`.
- Format when the task permits writes: `pnfmt <paths>`.
- Check project structure and formatting without writing: `pnfmt --lint <paths>`.
- Add missing default configuration when explicitly requested: `pnfmt --write-default-config <directory-or-editorconfig>`.

Exit code `0` means success. Exit code `1` means `--check` found changes or `--lint` found changes or diagnostics. Exit code `2` means a usage, path, or formatting error.

## EditorConfig settings

Every formatter is opt-in. A skipped file normally has no applicable enabled setting; do not enable formatting unless configuration changes are part of the task.

Project files:

- `pnfmt_sort_entries = true` sorts eligible properties and items while retaining evaluation-sensitive entries.
- `pnfmt_csproj_empty_lines_between_groups = <number>` controls blank lines between top-level groups.
- `pnfmt_csproj_sort_item_types = <names>` replaces the built-in sortable item-type list. Separate names with commas or semicolons, or use `*` for homogeneous item groups.
- Standard `indent_style`, `indent_size`, `tab_width`, and `end_of_line` settings control XML layout.

Resource files:

- `pnfmt_sort_entries = true` sorts resource entries.
- `pnfmt_resx_remove_xsd_schema = true` removes the embedded XSD schema.
- `pnfmt_resx_remove_documentation_comment = true` removes the standard documentation comment.
- `pnfmt_resx_sort_comparer` accepts `InvariantCulture`, `InvariantCultureIgnoreCase`, `OrdinalIgnoreCase`, or `Ordinal`.

INI and EditorConfig files:

- `pnfmt_sort_entries = true` sorts keys within property blocks. Blank lines do not split a block; comments, headers, and unknown lines do.
- `pnfmt_ini_group_by_prefix = true` sorts keys and separates prefixes shared by at least two keys. A prefix is the text before the first underscore; singleton prefixes get no extra blank lines.
- `pnfmt_ini_merge_groups = true` merges sections with the same header, ignoring case, and retains their body occurrence order.
- `pnfmt_ini_sort_groups = true` sorts named sections ordinally, ignoring case, while leaving the preamble first.

The INI formatter normalizes assignments to `key = value`, collapses repeated blank lines, and preserves comments and unknown lines. An `.editorconfig` containing `root = true` needs its own matching `[*.editorconfig]` section because it cannot inherit this configuration. Merging or sorting sections can change duplicate-section precedence; enable either only when safe.

Response files:

- `pnfmt_sort_entries = true` sorts non-empty physical lines ordinally, ignoring case with an ordinal tie-breaker.
- Blank lines do not split a sortable block. A full-line `#` comment does and remains in place.
- Compiler response arguments can be order-sensitive because later options may override earlier ones. Enable sorting only when safe, and use comment barriers around order-sensitive blocks.

Solution files:

- `pnfmt_sort_entries = true` orders known `.slnx` elements, uses two-space XML indentation, and preserves unknown extension elements as ordering barriers.

Legacy `csproj_formatter_*` and `resx_formatter_*` names remain fallback aliases. PNFmt reports warning `PNFMT001` when it uses or ignores one, and a matching `pnfmt_*` setting takes precedence.

## Repository configuration

The optional `.pnfmt` JSON file belongs at the Git repository root and currently supports one tool-level setting:

```json
{
  "maxCpuCount": 4
}
```

`maxCpuCount` must be positive and defaults to `1`; command-line concurrency options override it. Keep file-formatting behavior in `.editorconfig`.

## Default configuration

`--write-default-config` adds all-enabled PNFmt settings to `.editorconfig`. It reuses matching sections, preserves existing values and line order, inserts only missing keys at sorted positions, appends missing sections, and adds no comments. A new file gets `root = true`. Earlier marked PNFmt default blocks are removed and migrated to this structure.

The command also creates `.pnfmt` with `maxCpuCount` set to `4` when the file is missing and never modifies an existing `.pnfmt`. Inside Git, it targets the repository root; outside Git, it creates the file beside `.editorconfig`.

When legacy formatter settings exist, answer both migration questions explicitly in non-interactive runs:

```powershell
pnfmt --write-default-config --migrate-legacy-config=<true|false> --remove-legacy-config=<true|false> <directory-or-editorconfig>
```

Migration adds current `pnfmt_*` names with the legacy values in the same sections. Removal is independent; set it to `true` only when the task includes deleting the old names.

The generated defaults enable same-named group merging for `.editorconfig` and `.ini` files and response-file sorting. Review these changes because both behaviors can affect order-sensitive semantics.

## Completion

Inspect the diff and preserve unrelated changes. After formatting, rerun `--check` on the same scope and resolve failures caused by the requested change.
