---
name: pnfmt
description: Check, format, lint, or configure supported files with PNFmt.
---

# PNFmt

Use PNFmt only within the task's scope. It formats .NET project, solution, resource, response, INI, and EditorConfig files; it does not format C# source.

## Invocation

In this source repository, replace `pnfmt` in the commands below with:

```powershell
dotnet run --project PNFmt.Cli/PNFmt.Cli.csproj --configuration Release -- <pnfmt-arguments>
```

Elsewhere, use an installed `pnfmt` command. Do not install or update it unless requested.

## Operations

- Check without writing: `pnfmt --check <paths>`
- Format when the task permits writes: `pnfmt <paths>`
- Include unchanged files: `pnfmt --all <paths>`
- Validate project structure without writing: `pnfmt --lint <paths>`
- Add missing default configuration when explicitly requested: `pnfmt --write-default-config <directory-or-editorconfig>`

Inside a Git working tree, PNFmt selects only staged, unstaged, and untracked files within the requested scope. The default scope remains the current directory without recursion. Use `--all` only when the task calls for unchanged files, `--recursive` when it covers a directory tree, and `--file-pattern` when narrowing that tree.

## Configuration

PNFmt formatters are opt-in through `.editorconfig`. A skipped file usually has no enabled PNFmt setting; do not enable one unless configuration is part of the task.

The repository-root `.pnfmt` file contains tool settings such as `maxCpuCount`; `-m` overrides it. Keep file-formatting behavior in `.editorconfig`. The default-config command creates a missing `.pnfmt` with `maxCpuCount` set to `4` and never modifies an existing one.

The default-config command reuses matching EditorConfig sections, preserves existing values and line order, inserts only missing keys at sorted positions, and adds no comments. It also enables merging same-named INI groups by default. Review configuration changes because INI group merging and response-file sorting can affect order-sensitive files.

When legacy `csproj_formatter_*` or `resx_formatter_*` settings exist, answer both migration questions explicitly in non-interactive runs:

```powershell
pnfmt --write-default-config --migrate-legacy-config=true --remove-legacy-config=<true|false> <directory-or-editorconfig>
```

Migration preserves legacy values under current `pnfmt_*` names and in the same sections. Set removal to `true` only when the task includes cleaning up the old names.

## Completion

Inspect the diff and preserve unrelated changes. After formatting, rerun `--check` on the same scope and resolve failures caused by the requested change.

See [the repository documentation](../../../README.md) for supported settings and formatter-specific behavior.
