# PNForge builds

The repository's [verify pipeline](../.forge/pipelines/verify.json) runs on pushes
to `master` in PNForge and can also be started manually. PNForge uses `verify` as
this repository's required pull request check. Ready pull requests receive checks
automatically; pipeline JSON does not need a separate pull request trigger.
Feature branches use those PR checks or a manual run, avoiding a second standalone
build for the same feature-branch push.

Automatic pushes, schedules, ordinary manual runs and required PR checks finish
at **Build** (job **Package**). A successful standalone `master` push or manual
build can be continued through **Release** on demand. Its **Tag** job
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

[Invoke-ForgeBuild.ps1](../.forge/scripts/Invoke-ForgeBuild.ps1) assigns temporary drive
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

The job declares nine process steps: restore, .NET formatting, Release build,
repository formatting, gate tests, benchmark correctness, tests with coverage,
performance comparison, and packing with installed-tool validation. Each step
enters through the drive wrapper and calls the corresponding operation in
[Invoke-Validation.ps1](../build/Invoke-Validation.ps1). They share one checkout,
Release output and validation version, `0.0.0-validation.1`. Restore and build run
once; the test and pack commands use `--no-build --no-restore`. Building with that
version from the start ensures the installed CLI reports the package version.

All tests, per-assembly coverage thresholds, performance comparisons, packing and
isolated installed-tool checks remain enabled. The final package manifest must
identify the exact tested Git commit. Timing regressions fail on VELA; the hosted
GitHub builds retain their separate `ReportOnly` timing policy. The shared program
sets English .NET diagnostics and restores its process environment afterward.
The xUnit test assembly caps collection workers at 16 on every platform.

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
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\Invoke-Validation.ps1 -Version 0.0.0-validation.1
```

Generated results are ignored under `artifacts/`. Use a fresh checkout for each
build so earlier reports and packages cannot satisfy a later run. See
[performance gates](performance.md) for investigating a comparison failure.
`-Step` runs one named operation for pipeline orchestration or diagnosis; the
default `-Step All` is required for complete local validation. Shared build code
lives in `build/`, Forge adapters in `.forge/scripts/`, and GitHub policy in
`.github/`. Their README files describe the boundaries. The removed direct
publisher and nested Forge build wrapper are not needed by this sequence.

## Build/Deployment migration

The 2026-10-24 audit against PNForge 0.1.0.354 found schema 4 environment APIs and
protocol 5 support, but no supported environment authorization for this pipeline's
GitHub tag write. The active definition therefore retains schema 3. This is a
release-capability blocker, not an older-server compatibility requirement.

Keep one product pipeline: **Build**, technical ID `verify`, in the existing file.
Its **Build / Package** stage/job keep IDs `build` / `verify`; the legacy
**Release / Tag** stage/job keep IDs `tag` / `release-tag`. No deployment environment
is declared. Release tagging is a side effect that triggers NuGet publication;
representing it as a successful validation job or an unauthenticated environment
would change the authorization boundary.

Schema 4 can express the existing verification work by moving the unchanged
`build` stage into `build.stages`, removing legacy `stages`, `runThrough` and
`requiredCheck` fields, and requiring every Build job to succeed. A Build-only
conversion is technically supported, but activating it now would remove the
on-demand release action. Moving `release-tag` into Build would instead make
tagging part of every successful build. Neither change preserves this workflow.

Environment jobs prohibit authored `git` requests, manual gates, dependencies,
outputs and Build reports. They consume required immutable Build artifacts and
run only after an explicit environment deployment request. The current tag job
requires GitHub tag-write authentication through the executing account's Git
credential helper and writes a `release-tag` artifact. Forge's lease-bound Git
grants describe Forge repository IDs and an allowed write branch, not an external
GitHub remote or a create-only tag ref. There are no PNFmt Git grants to migrate;
adding a Forge branch-write grant would not authorize this operation. Do not omit
`git` and rely on the persistent GitHub helper to evade the environment restriction.

Before activation, Forge needs an approved way to authorize an external GitHub
create-only tag write for the exact captured successful Build and retain its
receipt. Revisit the live contract and artifact/provenance handoff then. Preserve
the package identity/source checks, current GitHub `master` check, annotated
create-only tag semantics and exact successful GitHub Build/package selection.
An environment must consume this Build's captured `package` without restoring,
compiling, retesting or packing, and must never depend on another environment.
Replacing the retained tag artifact also requires a supported durable receipt.

The audit found no schedules, upstream/dependent subscriptions, pipeline Git
grants or PNFmt-specific agent routes. The required check remains `verify`, the
default branch remains `master`, and Windows/.NET requirements, default-pool
routing, retention and server-owned policies are unchanged. Manual and master
push triggers remain; PR checks still come from repository settings. Any future
schedule still stops at Build. No automatic publication is added or removed:
ordinary builds never tag, and the existing release action still requires
deliberate continuation and approval. This migration does not authorize that action.

## Release tags on demand

The final stage restores the successful Build's exact `package` artifact into
`incoming/validated-package` in its fresh checkout. It calls
[New-ReleaseTag.ps1](../.forge/scripts/New-ReleaseTag.ps1), checks that package's source
commit, and reads the intended version from the project. The
[tag helper](../.forge/scripts/create-release-tag.cjs) checks the clean captured
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
