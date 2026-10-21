# PNForge ownership

`pipelines/verify.json` owns the controlled Windows validation on VELA and the
on-demand release-tag stage. Required PR checks and ordinary runs stop at Build.
Tag creation requires an eligible standalone master build and explicit approval.

Build has separate restore, .NET formatting, Release build, repository formatting,
gate-test, benchmark-correctness, coverage-test, performance, and package steps.
They share one checkout and version. Tests and packing reuse its Release binaries.
Every process enters through the Windows path wrapper; artifacts and native
reports remain job-level outputs. The pipeline always enforces performance timing.

`scripts/` contains Forge-only entry points, Windows workspace compatibility, and
tag creation. Shared validation and GitHub Build selection live in `build/` at
the repository root.

Forge retains its own validation package. It does not publish that package to
NuGet. The GitHub tag workflow consumes the GitHub Build's separately validated
release artifact. See [Forge builds](../docs/forge-build.md) and
[releases](../docs/releases.md) before changing either side of that boundary.
