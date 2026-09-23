# Repository formatting

After changes, run from the repository root:

```powershell
.\scripts\Format-Repository.cmd
.\scripts\Format-Repository.cmd -Check -NoBuild
```

On Linux or PowerShell 7, use `./scripts/Format-Repository.ps1` with the same arguments.

Use this script for all repository-wide formatting. It builds the local CLI and
formats source and configuration, including tests, while excluding fixture data
(`_files`, `_editor`), snapshots (`Snapshots/`), and build output. Never run PNFmt
recursively over the root: fixture `.editorconfig` files can override exclusions.

Review the diff. Preserve fixtures and snapshots unless the task explicitly requires
changing them; never regenerate snapshots just to accommodate formatting.

# Skill and documentation maintenance

Keep the PNFmt skill (`.agents/skills/pnfmt/SKILL.md` and its supporting references)
self-contained: bundle required guidance and keep all its documentation links
within `.agents/skills/pnfmt/`. When formatter features, CLI options, configuration,
defaults, diagnostics, or compatibility behavior change, update this skill,
`README.md`, and relevant `docs/` pages in the same change. Include activation
requirements, supported values, and preservation limits; distinguish defaults for
omitted settings from generated configuration. Verify examples and links before
considering the change complete.

# Commits and quality gates

Always commit completed work with descriptive messages after relevant checks.
Stage only related files and keep unrelated changes in separate commits.

For source changes, run appropriate tests and the complete validation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-GlobalTool.ps1 -Version 0.0.0-validation.1 -PackOnly
```

This runs Release tests, coverage, performance comparisons, packaging, and isolated
installed-tool checks without publishing. Keep the validation version short to avoid
Windows native-library path limits. Leave `-SkipTests`, `-SkipPack`, and
`-SkipPackageValidation` off. Fix failures and rerun until all gates pass; do not
lower coverage thresholds or bypass checks.

Keep the default `-TimingPolicy Enforce` locally. Before creating or pushing a
release tag, run the complete validation on VELA, the maintainer's controlled
Windows machine. Hosted Build and Publish workflows use `-TimingPolicy ReportOnly`:
timing regressions warn, but allocation regressions, invalid reports, and incorrect
or unstable output still fail the gate. Hosted results do not replace local validation.

Performance comparisons cannot be skipped. Investigate regressions against
`benchmarks/performance-baseline.json`; never widen tolerances to pass or advance
the baseline automatically. For an accepted feature cost, record old/new measurements
and the reason in `docs/performance.md`, then update the pinned revision in a separate,
descriptive commit. That document covers the comparison and baseline-update procedure.
