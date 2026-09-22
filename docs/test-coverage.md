# Test coverage

Coverage is collected from the xUnit/VSTest suite with Coverlet. It includes the
`PNFmt.Core` and `pnfmt` production assemblies, including the instrumented CLI
used by subprocess integration tests. Test code, dependencies, benchmarks,
SnapshotViewer, generated source, and code explicitly marked
`ExcludeFromCodeCoverage` are excluded. Compiler-generated code is not broadly
excluded, so async methods and iterators remain measurable.

CLI entry-point tests run in a collection that does not overlap other tests because
they temporarily change the process-wide current directory and console streams.
Other test collections retain their normal parallel execution.

## Run locally

From the repository root on Windows:

```powershell
./scripts/Test-Coverage.cmd
```

The wrapper uses Windows PowerShell with execution policy bypass for that process.
With PowerShell 7, including on Linux:

```powershell
./scripts/Test-Coverage.ps1
```

The command restores the pinned ReportGenerator tool, builds and tests in Release,
generates reports, and checks the configured minimums. Use `-NoBuild -NoRestore`
after a Release build when the project dependencies are already restored.
`-NoRestore` applies to the test command; the report tool is still restored.

Each run writes to a fresh `artifacts/coverage/<run-id>/` directory. The command
prints the HTML report path. Files include:

- `report/index.html`: coverage by assembly, class, file, line, and branch.
- `report/Summary.json`: integer counts used by the coverage gate.
- `report/SummaryGithub.md` and `report/CoverageGate.md`: coverage and threshold summaries.
- `results/`: raw Cobertura reports and TRX test results.

Reports are generated even if tests or thresholds fail, provided collection
produced coverage. Old reports cannot satisfy a new run's gate. Report history
is retained in `artifacts/coverage-history/`; it is separate from the inputs to
the gate. All generated artifacts are ignored by Git.

## Set minimum coverage

Edit `coverage-thresholds.json`. Each production assembly has independent line
and branch minimums, as percentages from 0 to 100. The enforced minimums are:

```json
{
  "PNFmt.Core": { "line": 95, "branch": 88 },
  "pnfmt": { "line": 92, "branch": 83 }
}
```

These minimums apply to both Windows and Linux builds and to release validation.
Missing reports, missing assemblies, invalid settings, and empty or invalid
coverage counts also fail. Raise the minimums as coverage improves, retaining a
small margin. Investigate regressions before lowering a minimum or excluding code.

The gate compares integer counts against each minimum without rounding; equality
passes. Strong coverage in one assembly cannot compensate for weak coverage in
the other. Any failed test also fails the command, regardless of coverage.

To check an existing report without rerunning tests:

```powershell
./scripts/Test-CoverageReport.ps1 -ReportPath artifacts/coverage/<run-id>/report/Summary.json
```

Use `-ThresholdsPath` to evaluate a candidate configuration. The gate's regression
checks run with `./scripts/Test-CoverageReport.Tests.ps1` and cover threshold
boundaries, independent assemblies/metrics, missing data, and malformed input.

## CI and releases

The Build workflow runs the shared coverage command on Windows and Linux. It adds
coverage and threshold results to the GitHub Actions summary and uploads a
`coverage-<os>` artifact even when a test or threshold fails. Download the artifact
and open the report's `index.html` to investigate uncovered code.

Successful default-branch coverage runs save ReportGenerator history to a cache
per operating system. Subsequent builds and pull requests restore that history
for the HTML trend charts. Cache expiration can shorten the available history;
the current build's coverage and gate do not depend on the cache.

## Initial measurement

On 2026-09-17, Windows with .NET SDK 10.0.302 and Release configuration passed all
1,793 tests and produced these results:

| Assembly | Covered lines | Line coverage | Covered branches | Branch coverage |
| --- | ---: | ---: | ---: | ---: |
| PNFmt.Core | 2,910 / 3,005 | 96.84% | 1,720 / 1,925 | 89.35% |
| pnfmt | 973 / 1,041 | 93.47% | 388 / 458 | 84.72% |

These observations informed the enforced minimums above, leaving roughly one to
two percentage points of headroom for each metric.

## Release and merge enforcement

`Publish-GlobalTool.ps1` uses the same command before packing. The release workflow
uploads a `coverage-release` artifact, and a failed check prevents publication.
`-SkipTests` remains an explicit bypass for callers that have already validated
the same source, such as the Build workflow's later packaging step.
It does not skip the separate [performance gate](performance.md), which runs on
every publish-script invocation, including `-PackOnly`.

The repository's [Required build checks ruleset](https://github.com/stefanegli/PNFmt/rules/23625522)
requires these GitHub Actions checks on the default branch to block merges when
coverage validation fails:

- `Build, test, and validate package (ubuntu-latest)`
- `Build, test, and validate package (windows-latest)`

The checks must come from GitHub Actions, and pull requests must be tested against
the latest default-branch code. Direct pushes also need passing checks on the
commit, so push new commits to a working branch first.

A plain `dotnet build` only compiles. Coverage enforcement happens in the test
command and in CI/release validation.
