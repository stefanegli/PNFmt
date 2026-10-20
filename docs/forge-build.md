# PNForge builds

The repository's [verify pipeline](../.forge/pipelines/verify.json) runs on every
branch push to PNForge and can also be started manually. PNForge uses `verify` as
this repository's required pull request check. Ready pull requests receive checks
automatically; pipeline JSON does not need a separate pull request trigger.

The single build job requires an ordinary Windows agent advertising `os: windows`
and `dotnetSdk: 10.0`. The configured default agent runs on VELA. It needs Git,
.NET 10, Node.js for the release-selection tests, Windows PowerShell, and NuGet
access for dependencies and the pinned report tool. No privileged agent route or
publication credential is needed. Forge creates an isolated checkout of the exact
source revision, including the history needed for pinned performance baselines,
and supplies its agent-owned temporary directory.

The job calls [Test-ForgeBuild.ps1](../scripts/Test-ForgeBuild.ps1). It restores
dependencies, checks `dotnet format` including unresolved project references,
builds Release, verifies repository formatting, checks the coverage and release
package validators, runs the Node.js release-selection tests and benchmark
correctness cases, then runs the complete `Publish-GlobalTool.ps1` validation with
`-Version 0.0.0-validation.1 -PackOnly -TimingPolicy Enforce`. All tests, per-assembly
coverage thresholds, performance comparisons, packing and isolated installed-tool
checks remain enabled. The final package manifest must identify the exact tested
Git commit. Timing regressions fail on VELA; the hosted GitHub builds retain their
separate `ReportOnly` timing policy.

Forge imports the TRX results and Cobertura coverage into its native Tests and
Results views. The existing coverage script enforces
[coverage-thresholds.json](../coverage-thresholds.json); Forge additionally requires
complete test and coverage reports for both production assemblies. Coverage and
performance directories are retained even after a failed job when available.
The package artifact is retained only after successful verification. Artifacts
from the latest ten completed runs with artifacts are kept; run history and logs
have their own server retention policy.

To reproduce the job from a clean checkout on VELA:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-ForgeBuild.ps1
```

Generated results are ignored under `artifacts/`. Use a fresh checkout for each
build so earlier reports and packages cannot satisfy a later run. See
[performance gates](performance.md) for investigating a comparison failure.

The Forge package is a validation artifact. NuGet publication continues through
the existing GitHub version-tag workflow and its exact-commit `release-package`
artifact. See [releasing PNFmt](releases.md); a successful Forge build alone does
not supply that GitHub artifact or publish a package.
