# PNForge ownership

`pipelines/verify.json` owns the controlled Windows validation on VELA and the
on-demand release-tag stage. Required PR checks and ordinary runs stop at Build.
Tag creation requires an eligible standalone master build and explicit approval.

`scripts/` contains Forge-only entry points, Windows workspace compatibility, and
tag creation. Shared validation and GitHub Build selection live in `build/` at
the repository root.

Forge retains its own validation package. It does not publish that package to
NuGet. The GitHub tag workflow consumes the GitHub Build's separately validated
release artifact. See [Forge builds](../docs/forge-build.md) and
[releases](../docs/releases.md) before changing either side of that boundary.
