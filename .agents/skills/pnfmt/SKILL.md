---
name: pnfmt
description: Format or check .NET project, solution, resource, response, INI, and EditorConfig files with PNFmt. Use after changing supported files, when preparing formatting checks, when investigating PNFmt diagnostics, or when asked to create PNFmt configuration. Do not use for C# source formatting.
---

# Use PNFmt

Run PNFmt only on files and directories within the task's scope. Inspect the resulting diff and preserve unrelated user changes.

## Choose the command

When working in the PNFmt source repository, invoke the current implementation:

```powershell
dotnet run --project PNFmt.Cli/PNFmt.Cli.csproj --configuration Release -- <pnfmt-arguments>
```

In other repositories, use an installed `pnfmt` command. If it is unavailable, report that clearly instead of installing or updating it without permission.

## Check or format

Use `--check` for review-only work or before deciding whether a write is needed:

```powershell
pnfmt --check path/to/Project.csproj path/to/Strings.resx
```

When the task authorizes changes, format the supported files that were changed:

```powershell
pnfmt path/to/Project.csproj path/to/Strings.resx
```

Use `--recursive` only when the requested scope is a directory tree. Prefer explicit paths or `--file-pattern` when a repository contains unrelated files.

Use `--lint` when the task includes project-structure validation. It implies a formatting check and does not write files:

```powershell
pnfmt --lint path/to/Project.csproj
```

Interpret exit codes as follows:

- `0`: the command succeeded and no check failures remain.
- `1`: `--check` found formatting changes, or `--lint` found changes or diagnostics.
- `2`: command usage, path resolution, or formatting failed.

After a write, rerun `--check` on the same scope and review the diff.

## Configuration

PNFmt formatters are opt-in through `.editorconfig`. A skipped file usually means no applicable PNFmt setting is enabled. Do not change configuration merely to avoid a skipped result unless configuration is part of the task.

When asked to enable all current formatters and cleanup options, write the managed default block:

```powershell
pnfmt --write-default-config <repository-or-editorconfig-path>
```

The command preserves rules outside the marked PNFmt block and can be run again to refresh that block. Review the `.editorconfig` diff because options such as INI section sorting and response-file sorting can change order-sensitive files.

See [the repository documentation](../../../README.md) for supported settings and formatter-specific behavior.
