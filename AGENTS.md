# Repository formatting

After making changes, run the repository formatter from the repository root before
finishing the task:

```powershell
.\scripts\Format-Repository.cmd
```

On Linux or with PowerShell 7, use `./scripts/Format-Repository.ps1` instead.
The command builds the local PNFmt CLI and formats source and configuration files,
including C# test code.

Verify the result with `-Check -NoBuild`, and review the diff before finishing.
Run the tests appropriate to any source changes.

Always use this script for repository-wide formatting. Do not run PNFmt recursively
over the repository root: fixture `.editorconfig` files can explicitly re-enable
formatting and override the root exclusions. The script excludes fixture data
(`_files`, `_editor`), expected snapshots (`Snapshots/`), and build output before
invoking PNFmt.

Keep fixture and snapshot contents unchanged unless the task explicitly requires
changing them. Do not regenerate expected snapshots to accommodate formatting.
