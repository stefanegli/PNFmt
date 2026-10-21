# PNForge builds

The repository's [verify pipeline](../.forge/pipelines/verify.json) runs on pushes
to `master` in PNForge and can also be started manually. PNForge uses `verify` as
this repository's required pull request check. Ready pull requests receive checks
automatically; pipeline JSON does not need a separate pull request trigger.
Feature branches use those PR checks or a manual run, avoiding a second standalone
build for the same feature-branch push.

Automatic pushes, schedules, ordinary manual runs and required PR checks finish
at **Build, test and package**. A successful standalone `master` push or manual
build can be continued through **Create release tag** on demand. That final job
also requires explicit approval; PRs and feature branches cannot execute it.
**Run all stages** explicitly authorizes both validation and tagging for a new
eligible run. Normal builds never create tags.

The single build job requires an ordinary Windows agent advertising `os: windows`
and `dotnetSdk: 10.0`. The configured default agent runs on VELA. It needs Git,
.NET 10, Node.js for the release-selection tests, Windows PowerShell, and NuGet
access for dependencies and the pinned report tool. No privileged agent route or
publication credential is needed. Forge creates an isolated checkout of the exact
source revision, including the history needed for pinned performance baselines,
and supplies its agent-owned temporary directory.

[Invoke-ForgeBuild.ps1](../scripts/Invoke-ForgeBuild.ps1) assigns temporary drive
aliases to the existing workspace and temporary directory containers before
starting validation. Both directories keep their own folder below the aliased
drive root so repository discovery, local tools and temporary cleanup guards work.
This avoids libgit2's Windows ancestor lookup failing beneath Forge's protected installation
folders and shortens native-library paths. Files remain in the assigned agent
directories with their existing permissions. The wrapper restores the environment
and removes only its own unchanged aliases after normal success or failure. After
forced process termination, review any remaining mapping under the agent identity
before removing it. Two unused drive letters are required. The pipeline selects
English .NET/MSBuild diagnostics so its unresolved-reference check also works
under the service account's locale.

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

## Release tags on demand

The final stage restores the successful Build's exact `package` artifact into
`incoming/validated-package` in its fresh checkout. It calls
[New-ReleaseTag.ps1](../scripts/New-ReleaseTag.ps1), checks that package's source
commit, and reads the intended version from the project. The
[tag helper](../.github/scripts/create-release-tag.cjs) checks the clean captured
checkout, requires the exact commit at GitHub's current `master`, and applies the
same successful default-branch Build/artifact selection as the publish workflow.
It creates and pushes only the annotated `v<version>` tag to
`https://github.com/stefanegli/PNFmt.git`, then retains a `release-tag` receipt with
the source commit, tag and Forge/GitHub build identities. It does not advance a
branch, bump the version, rebuild or publish a NuGet package itself.

An existing annotated tag at the same commit is reported without another push;
a tag with another identity or a lightweight tag fails. The create-only push
also rejects concurrent tag creation instead of replacing it. On an ambiguous
transport failure, inspect the actual tag and GitHub Publish run before retrying.

Git uses its credential helper under the executing service identity. The default
agent runs as `NT SERVICE\PNForge.Agent.default`; an interactive user's Git login
does not provision that account. An administrator must provision repository-scoped
GitHub tag-write access for the agent (or a separately routed release agent) using
its Git credential helper. Keep credentials outside the repository, pipeline JSON,
arguments and logs. The script disables interactive prompts; missing access fails
the tag push. Do not grant server repository write access or use the elevated
deployment agent just to create GitHub tags. Ordinary validation needs none of
these publication credentials. Public GitHub Build metadata is read anonymously;
API errors and rate limits block tagging until resolved.

The Forge package remains a validation artifact. The newly pushed GitHub tag
triggers the existing NuGet workflow, which downloads the separate exact-commit
GitHub `release-package`. A successful Forge build alone cannot supply that
artifact. See [releasing PNFmt](releases.md) for the complete procedure.
