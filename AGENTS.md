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

Always commit completed work in useful, cohesive increments after relevant checks.
Each commit should be independently understandable and leave the repository in a
working state. Use descriptive messages that explain the change and its purpose.
Commit a finished increment before starting the next unrelated change; do not
defer all commits until the end of a task or create arbitrary checkpoint commits.
Stage only related files and keep unrelated changes in separate commits. Preserve
other contributors' work and do not rewrite published commits without an explicit
request.

For source changes, run appropriate tests and the complete validation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\Invoke-Validation.ps1 -Version 0.0.0-validation.1
```

This runs Release tests, coverage, performance comparisons, packaging, and isolated
installed-tool checks without publishing. Keep the validation version short to avoid
Windows native-library path limits. Use the default `-Step All`; individual
`-Step` selections are for pipeline orchestration and focused diagnosis, not full
validation. Fix failures and rerun until all gates pass; do not lower coverage
thresholds or bypass checks. A successful exact-commit Forge run on VELA executes
the same full sequence and supplies this validation; avoid running another full
suite concurrently with its performance measurements.

Shared checks belong in `build/`; Forge-only adapters and tagging belong in
`.forge/scripts/`; GitHub orchestration belongs in `.github/workflows/`. Read
`build/README.md`, `.forge/README.md`, and `.github/WORKFLOWS.md` before changing
their boundary. Keep workflow documentation in `.github/WORKFLOWS.md`; a README
inside `.github/` takes precedence over the root product README on GitHub.
GitHub Build must remain read-only and validate the same release package on both
required platforms. Only
the tag workflow may obtain NuGet OIDC credentials, after checking the retained
exact-commit Build package. Preserve its filename, environment, required check
names, and gate-before-credentials order. Do not replace its artifact with a
Forge validation package without implementing the authenticated handoff and
migrating repository rules together.

Forge's `verify` pipeline is displayed as Build. Retain its schema 3 gated
Release/Tag path until the environment contract supports authorized external
GitHub tag writes and durable receipts; see `docs/forge-build.md`. Schema 4
environment jobs prohibit authored Git access. Do not hide Git access behind
persistent credentials, move tagging into Build, or drop the release action to
force a schema upgrade. Preserve stable IDs, required checks, routes and grants.

Keep the default `-TimingPolicy Enforce` locally. Before creating or pushing a
release tag, run the complete validation on VELA, the maintainer's controlled
Windows machine. Hosted Build uses `-TimingPolicy ReportOnly`:
timing regressions warn, but allocation regressions, invalid reports, and incorrect
or unstable output still fail the gate. Hosted results do not replace local validation.
Tag publication reuses the successful Build's validated package for the exact
commit. Do not create release branches or repeat validation in the tag workflow;
follow `docs/releases.md`.

Performance comparisons cannot be skipped. Investigate regressions against
`benchmarks/performance-baseline.json`; never widen tolerances to pass or advance
the baseline automatically. For an accepted feature cost, record old/new measurements
and the reason in `docs/performance.md`, then update the pinned revision in a separate,
descriptive commit. That document covers the comparison and baseline-update procedure.
