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

# Skill and documentation maintenance

Keep `.agents/skills/pnfmt/SKILL.md`, its supporting references, `README.md`, and
the relevant `docs/` pages aligned with the implementation. When changing a
formatter feature, CLI option, configuration setting, default, diagnostic, or
compatibility behavior, update the affected skill guidance and documentation in
the same coherent increment. Include activation requirements, defaults, supported
values, and preservation limits where they affect usage. Verify examples and
links, and distinguish defaults for omitted settings from generated configuration.
Do not consider a user-facing feature complete while its guidance is stale.

# Commits and quality gates

Always commit completed work in useful, coherent increments with descriptive
commit messages. Keep unrelated changes in separate commits, stage only the
files belonging to each increment, and run the relevant checks before committing.

Before finishing source changes, run the publish script's quality gates:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-GlobalTool.ps1 -Version 0.0.0-validation.1 -PackOnly
```

Use a short validation version, as above, to keep isolated Windows package paths
within native library loader limits. `-PackOnly` runs the Release
tests, coverage thresholds, performance regression comparisons, package build, isolated tool installation, and
installed-tool checks without publishing. Leave `-SkipTests`, `-SkipPack`, and
`-SkipPackageValidation` off. Fix failures and rerun until all gates pass;
do not lower coverage thresholds or bypass checks to make a change pass.

The performance gate has no skip switch. Keep the pinned baseline and tolerances
in `benchmarks/performance-baseline.json` reviewable. Investigate regressions;
do not widen tolerances to make a failure pass. When accepting an intentional
feature cost, record the old/new measurements and reason in `docs/performance.md`,
then update the pinned revision in a separate, descriptive commit. Never advance
the baseline automatically. See `docs/performance.md` for the comparison method.
