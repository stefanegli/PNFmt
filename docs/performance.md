# Performance gates

`scripts/Publish-GlobalTool.ps1` runs a mandatory performance gate before packing or pushing. It runs in `-PackOnly` mode and also when `-SkipTests`, `-SkipPack`, or `-SkipPackageValidation` are supplied. There is no performance skip switch. The Build workflow runs it on Windows and Linux; the release workflow runs it before publication.

Run it independently from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Performance.ps1
```

With PowerShell 7, including on Linux, use `./scripts/Test-Performance.ps1`. Allow several minutes and avoid other builds, tests, or CPU/disk-heavy work during measurement. A Git checkout with the pinned baseline commit available locally is required. CI checks out full history. The script never fetches or advances the baseline automatically.

## What is compared

The gate builds the **same current benchmark sources** twice in Release: once against the current formatter/CLI source, once against the commit pinned in [`benchmarks/performance-baseline.json`](../benchmarks/performance-baseline.json). Both run on the same machine, runtime, architecture, and logical CPU count. This avoids a fixed millisecond budget tied to one developer's hardware. A missing baseline, failed build, invalid report, missing/duplicate case, incomplete sample set, or non-repeatable formatter output fails the gate.

There are 59 individually checked cases:

| Family | Cases |
| --- | --- |
| XML, XAML, MSBuild | 1,000 attribute-rich elements: default layout, 120-column wrapping, and one attribute per line; initial preview and already formatted check for each |
| C# | 200 methods with long lists: defaults, 120-column wrapping, `wrap_if_long`, `chop_always`, the three Microsoft blank-line preferences, and file headers; preview and already formatted check |
| Repository | Dependency sorting at 500/1,000/2,000/4,000 entries, plus discovery and serial/parallel preview/write/unchanged checks for small projects, mixed formats, and nested configuration |

Repository cases use 128 files, at least three operations/300 ms of warm-up, and seven measured samples. Read-only samples batch operations to target 100 ms; writes remain individual operations so input restoration stays outside the timed section. Formatter cases warm up for at least ten operations and 300 ms, then collect nine batches targeting 100 ms each. Each family runs in its own process. Three rounds alternate baseline/current order; the gate uses each run's median, then the median of all three runs. One interrupted process therefore cannot dominate the result, while a slowdown repeated in two runs still fails. Tiered JIT compilation is disabled for the measurement processes to reduce optimization/warm-up variance; this is a controlled comparison, not a promise of exact production throughput. The environment setting is restored afterward.

Managed allocation measurements include all threads. Setup, restoring write inputs, validation, process startup, builds, and console output are outside the timed sections. Repository writes include normal staged writes and storage flushes. Preview checks verify unchanged input bytes, and repeated formatting must be stable. Already formatted inputs can differ in size between feature configurations. Synthetic fixtures do not cover every repository or pathological nesting pattern.

## Limits and reports

Each case must independently satisfy both limits. Improvements elsewhere cannot compensate for a failing case.

| Metric | Allowed increase over baseline |
| --- | --- |
| Elapsed time | 20%, or 0.1 ms, whichever is larger |
| Elapsed time for write phases | 50%, or 0.1 ms, whichever is larger, to allow storage/flush variability |
| Managed allocations | 10%, or 4,096 bytes, whichever is larger |

Equality passes; comparisons use unrounded numbers. Floors handle small absolute differences, and the timing margins accommodate ordinary machine noise. All changes are reported, including those below the failure threshold and improvements. Output fingerprints are reported as unchanged or **changed**, so accepted behavior changes remain visible without assuming that all future features produce identical text.

Each invocation writes fresh files to `artifacts/performance/<run-id>/`:

- `PerformanceGate.md`: baseline/current timings, allocations, percentage changes, output differences, and pass/fail for every case. Retained on threshold failures.
- `baseline.json` and `run.json`: the exact policy, baseline/current commit IDs, dirty-working-tree flag, start time, and benchmark source hashes.
- Worker JSON reports: runtime metadata, sample arrays, iteration counts, and output fingerprints.
- Build and worker logs, retained even when a command fails before comparison.

CI adds the table to the GitHub Actions summary and uploads `performance-<os>` or `performance-release` artifacts, including on failure. The published package is blocked when the gate fails. The release workflow validates once in `-PackOnly` mode, then passes the returned package path to `dotnet nuget push` after authentication; it does not rebuild the package or repeat the gate. Direct publish-script invocations with skip switches still perform a fresh comparison. Older reports cannot satisfy a new publish run.

The separate standalone [full and quick benchmark runs](../benchmarks/README.md) remain useful for exploration and correctness smoke tests; they do not enforce timing limits. Gate comparison logic has deterministic regression checks:

```powershell
./scripts/Test-PerformanceReport.Tests.ps1
```

## Intentional baseline changes

Keep the baseline fixed across ordinary commits so small costs cannot accumulate unnoticed. If a feature intentionally costs more:

1. Run against the existing baseline, inspect the per-case report, and rule out accidental work or machine contention. Retain the report, including the failure when applicable.
2. Record old/new timing and allocation measurements, affected scenarios, and the justification here. Commit the implementation with its correctness checks.
3. In a separate commit, update `Revision` to that implementation's full commit ID and explain the accepted cost in `Reason`. Reviewers should see both the code and the baseline change. Do not widen tolerances merely to pass.
4. Rerun the complete publish validation against the new baseline.

Adding or substantially changing a scenario also requires reviewing the fixture and comparison semantics. Changes to the report schema/case contract must update `SuiteVersion`, the runner/comparator, and their regression tests together. The harness must build against the pinned source revision; if API compatibility changes, document and review the necessary baseline migration explicitly.

## Initial baseline and optimization

The initial pinned baseline is `09b320fd07321f65cd341d58b38f1d22be670c8b`, after the XML/XAML optimization. It locks in the improvement rather than allowing a later regression up to the original wrapping implementation's cost.

A same-machine Release comparison with `d3ac668`, using .NET 10.0.10 on Windows and tiered compilation disabled, measured these 120-column wrapping results for 1,000 elements. These are one-run medians of nine batches, not universal throughput estimates:

| Case | Before ms/file | Optimized ms/file | Allocation reduction |
| --- | ---: | ---: | ---: |
| XML preview | 5.61 | 4.19 | 36.7% |
| XML already formatted | 5.83 | 4.09 | 41.2% |
| XAML preview | 4.86 | 3.17 | 40.8% |
| XAML already formatted | 5.08 | 3.25 | 44.4% |
| MSBuild preview | 6.53 | 7.00 | 4.1% |
| MSBuild already formatted | 6.67 | 6.96 | 4.2% |

All output fingerprints matched. XML/XAML wrapping now uses the existing layout render instead of building and parsing an intermediate document, and the attribute writer reuses indentation and unchanged whitespace slices. Default formatting remained close to its previous cost. MSBuild shares the allocation improvements but retains its post-serialization wrapping pass; no MSBuild timing improvement is claimed. C# behavior and implementation are unchanged by this optimization and are now covered by the publish gate.
