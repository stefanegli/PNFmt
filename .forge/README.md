# PNForge ownership

`pipelines/verify.json` owns the controlled Windows validation on VELA and the
on-demand release-tag stage. Required PR checks and ordinary runs stop at Build.
Tag creation requires an eligible standalone master build and explicit approval.
The display names are **Build / Build / Package** and **Release / Tag**; pipeline,
stage and job IDs remain `verify`, `build`, `verify`, `tag` and `release-tag`.
Schema 3 is retained because schema 4 environment jobs prohibit authored Git
access and Forge Git grants do not authorize external GitHub tag writes. Keep
tagging out of Build; do not use the service account's persistent GitHub helper
to bypass the environment contract. See the
[migration audit](../docs/forge-build.md#builddeployment-migration) before activation.

Build has separate restore, .NET formatting, Release build, repository formatting,
gate-test, benchmark-correctness, coverage-test, performance, and package steps.
They share one checkout and version. Tests and packing reuse its Release binaries.
Every process enters through the Windows path wrapper; artifacts and native
reports remain job-level outputs. The pipeline always enforces performance timing.
The xUnit assembly limits collection workers to 16 in Forge, GitHub and local runs.

`scripts/` contains Forge-only entry points, Windows workspace compatibility, and
tag creation. Shared validation and GitHub Build selection live in `build/` at
the repository root.

Forge retains its own validation package. It does not publish that package to
NuGet. The GitHub tag workflow consumes the GitHub Build's separately validated
release artifact. See [Forge builds](../docs/forge-build.md) and
[releases](../docs/releases.md) before changing either side of that boundary.
