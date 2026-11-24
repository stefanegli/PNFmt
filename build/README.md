# Shared build and validation code

This folder contains repository checks and package validation used by GitHub
Actions and local maintenance. GitHub orchestration belongs in
`.github/workflows/`; developer formatting wrappers remain in `scripts/`.

Run the complete local gate with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\Invoke-Validation.ps1 -Version 0.0.0-validation.1
```

The default `-Step All` restores once, checks formatting, builds Release with the
requested version, tests the gates, checks benchmark correctness, runs tests with
coverage, compares performance, packs without rebuilding, and validates the
installed package. There are no publication credentials, publishing commands, or
skip switches. `-Step` selects one operation for pipeline orchestration;
running an individual step does not approve a release. Local timing defaults to
`Enforce`; hosted jobs use `ReportOnly` explicitly.

- `Test-Coverage*.ps1` collects coverage and enforces the independent production
  assembly thresholds.
- `Test-Performance*.ps1` compares the current formatter with the pinned revisions
  and enforces allocation, timing, correctness, and stability policy.
- `Test-ReleasePackage*.ps1` checks package identity, version, and source commit.
- `Test-ToolPackage.ps1` installs an existing package in isolation and checks its
  preview, formatting, and idempotence behavior.
- `select-github-release-build.cjs` selects the exact successful default-branch
  GitHub Build and retained package for release.

The gate-test step discovers shared `build/*.test.cjs` checks and hidden platform
adapter `*/scripts/*.test.cjs` checks through Git, including new untracked files.
An exported checkout runs its included adapters. Viewer checks, which require
separately generated HTML fixtures, retain their own workflow.

See [release policy](../docs/releases.md)
for orchestration, artifact ownership, credentials, and publication requirements.
