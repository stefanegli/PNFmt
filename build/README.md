# Shared build and validation code

This folder contains repository checks and package validation used by PNForge,
GitHub Actions, and local maintenance. Platform-specific entry points belong in
`.forge/scripts/` or `.github/workflows/`; developer formatting wrappers remain in
`scripts/`.

Run the complete local gate with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\Invoke-Validation.ps1 -Version 0.0.0-validation.1
```

The default `-Step All` restores once, checks formatting, builds Release with the
requested version, tests the gates, checks benchmark correctness, runs tests with
coverage, compares performance, packs without rebuilding, and validates the
installed package. There are no publication credentials, publishing commands, or
skip switches. `-Step` selects one operation for the explicit Forge pipeline;
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
  GitHub Build and retained package for release; both platforms use that policy.

See [Forge builds](../docs/forge-build.md) and [release policy](../docs/releases.md)
for orchestration, artifact ownership, credentials, and publication requirements.
