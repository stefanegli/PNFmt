# Performance gates

`scripts/Publish-GlobalTool.ps1` runs mandatory performance comparisons before packing or pushing. They run in `-PackOnly` mode and also when `-SkipTests`, `-SkipPack`, or `-SkipPackageValidation` are supplied. There is no performance skip switch. Allocation limits, correctness checks, report validity, and output stability remain enforced everywhere. Timing enforcement depends on the explicit policy below.

## Timing policy and release machine

`Publish-GlobalTool.ps1`, `Test-Performance.ps1`, and `Test-PerformanceReport.ps1` accept `-TimingPolicy`:

| Value | Behavior |
| --- | --- |
| `Enforce` (default) | Timing or allocation regressions fail the gate. Use for controlled local validation. |
| `ReportOnly` | Timing regressions produce warnings and remain visible in the report; allocation regressions and invalid or unstable results still fail. Used explicitly by the hosted Build workflow. Tag publication reuses that successful validation for the exact commit. |

The policy is not inferred from environment variables or machine names. Both modes run all 69 cases with the same samples, pinned baseline, and tolerances. Hosted runners can experience changing CPU contention and I/O latency even when baseline and current code run on the same machine. Their timing results are advisory, not a release timing approval.

**VELA**, the maintainer's local Windows machine, is the designated controlled environment. Before creating or pushing a release tag, run the complete validation there on the release candidate:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-GlobalTool.ps1 -Version 0.0.0-validation.1 -PackOnly
```

Leave `-TimingPolicy` at its `Enforce` default and leave all skip switches off. Avoid other builds, tests, or CPU/disk-heavy work during measurement. Investigate failures and retain the report; do not use report-only mode to approve a local release. This is a maintainer release requirement: the hosted workflow does not attest that validation on VELA occurred. A different controlled machine requires an explicit policy decision.

The [PNForge verify pipeline](forge-build.md) runs this complete validation on
VELA with `-TimingPolicy Enforce` and retains its performance reports. A successful
VELA run supplies the local validation for its exact source commit. The GitHub Build
and its retained release package remain separately required for tag publication.

Run just the performance comparison independently from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Performance.ps1
```

With PowerShell 7, including on Linux, use `./scripts/Test-Performance.ps1`. Allow several minutes and avoid other builds, tests, or CPU/disk-heavy work during measurement. A Git checkout with the pinned baseline commit available locally is required. CI checks out full history. The script never fetches or advances the baseline automatically.

The Build workflow sets `TEMP` and `TMP` to GitHub's `runner.temp` directory for performance comparisons and installed-package validation. On Windows this places both benchmark harnesses and repository fixtures on the runner's scratch volume. Local runs retain the caller's temporary directory. Durable file writes, sample counts, the pinned baseline, and regression limits are unchanged.

During alpha.10 release validation, three Windows hosted-runner attempts using the system-volume temporary directory failed serial repository-write comparisons despite identical formatter/CLI source apart from the package version. In the first run, small-project writes measured 1,235 ms for the baseline and 2,556 ms for the current version; the baseline's final round also slowed to 2,778 ms. A second run passed that case at 1,159/1,161 ms but failed mixed-format writes at 1,190/2,675 ms, with the baseline's final round at 2,688 ms. Allocations remained effectively unchanged. These measurements motivated using the runner's scratch directory for both sides of the comparison, without accepting a formatter cost or changing the baseline.

Scratch storage did not eliminate hosted-runner timing variability. The same alpha.10 commit passed Windows validation, then failed three timing comparisons in the master build while allocations and output checks passed. Hosted timing is therefore report-only; VELA retains timing enforcement. Neither the pinned revision nor the tolerance values changed with this policy.

## What is compared

The gate builds the **same current benchmark sources** in Release against the current formatter/CLI and both fixed references in [`benchmarks/performance-baseline.json`](../benchmarks/performance-baseline.json). The existing 67 cases use `Revision`; only the two new `csharp-compaction` cases use `CompactionRevision`, whose implementation supports that feature. All run on the same machine, runtime, architecture, and logical CPU count. This avoids comparing compaction with an older implementation that ignores its setting, or using a fixed millisecond budget tied to one developer's hardware. A missing baseline, failed build, invalid report, missing/duplicate case, incomplete sample set, or non-repeatable formatter output fails the gate. Compaction additionally requires identical output fingerprints across both revisions.

There are 69 individually checked cases:

| Family | Cases |
| --- | --- |
| XML, XAML, MSBuild | 1,000 attribute-rich elements: default layout, 120-column wrapping, and one attribute per line; initial preview and already formatted check for each |
| C# | 200 methods with long lists: defaults, 120-column wrapping, `wrap_if_long`, `chop_always`, the three Microsoft blank-line preferences, and file headers; plus 400 short multiline calls with compaction enabled; preview and already formatted check |
| RESX | 1,000 resources: LF and CRLF layout with single-line values, CRLF with multiline values, and CRLF with longer multiline values; preview and already formatted check |
| Repository | Dependency sorting at 500/1,000/2,000/4,000 entries, plus discovery and serial/parallel preview/write/unchanged checks for small projects, mixed formats, and nested configuration |

Repository cases use 128 files, at least three operations/300 ms of warm-up, and seven measured samples. Read-only samples batch operations to target 100 ms; writes remain individual operations so input restoration stays outside the timed section. Formatter cases warm up for at least ten operations and 300 ms, then collect nine batches targeting 100 ms each. Each family runs in its own process. Three rounds alternate baseline/current order; the gate uses each run's median, then the median of all three runs. One interrupted process therefore cannot dominate the result, while a slowdown repeated in two runs still fails. Tiered JIT compilation is disabled for the measurement processes to reduce optimization/warm-up variance; this is a controlled comparison, not a promise of exact production throughput. The environment setting is restored afterward.

Managed allocation measurements include all threads. Setup, restoring write inputs, validation, process startup, builds, and console output are outside the timed sections. Repository writes include normal staged writes and storage flushes. Preview checks verify unchanged input bytes, and repeated formatting must be stable. Already formatted inputs can differ in size between feature configurations. Synthetic fixtures do not cover every repository or pathological nesting pattern.

## Limits and reports

Each case must independently satisfy allocation limits and, in `Enforce` mode, timing limits. Improvements elsewhere cannot compensate for a failing case. In `ReportOnly` mode the same timing thresholds identify warnings.

| Metric | Allowed increase over baseline |
| --- | --- |
| Elapsed time | 20%, or 0.1 ms, whichever is larger |
| Elapsed time for write phases | 50%, or 0.1 ms, whichever is larger, to allow storage/flush variability |
| Managed allocations | 10%, or 4,096 bytes, whichever is larger |

Equality passes; comparisons use unrounded numbers. Floors handle small absolute differences, and the timing margins accommodate ordinary machine noise. All changes are reported, including those below the failure threshold and improvements. Output fingerprints are reported as unchanged or **changed**, so accepted behavior changes remain visible without assuming that all future features produce identical text.

Each invocation writes fresh files to `artifacts/performance/<run-id>/`:

- `PerformanceGate.md`: the timing policy, baseline/current timings, allocations, percentage changes, output differences, and `PASS`, `FAIL`, or `TIMING WARNING` for every case. An allocation failure stays `FAIL` even when timing is also advisory. Retained on threshold failures.
- `baseline.json` and `run.json`: the exact policy, timing mode, machine name, both reference commit IDs and current commit ID, dirty-working-tree flag, start time, and benchmark source hashes.
- Worker JSON reports: runtime metadata, sample arrays, iteration counts, and output fingerprints.
- Build and worker logs, retained even when a command fails before comparison.

CI adds the table to the GitHub Actions summary and uploads `performance-<os>` artifacts, including on failure. Allocation regressions, invalid reports, and correctness or output-stability failures block publication; hosted timing warnings do not. Build creates a single versioned `release-package` artifact and runs all gates on Windows and Linux, including installing the same package on both systems. A release tag selects a successful default-branch Build for the exact tagged commit and publishes its retained package after verifying package identity, version, and source commit. It does not rebuild or repeat the gates. A different commit, a PR build, a failed or incomplete run, or a missing/expired package cannot satisfy publication. See [releasing PNFmt](releases.md) for recovery. Direct publish-script invocations with skip switches still perform a fresh comparison and default to timing enforcement.

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

Suite version 2 adds eight RESX cases without changing the pinned source revision or tolerances. Previous mixed-repository fixtures use LF and do not exercise CRLF-specific serialization costs. The RESX cases compare both revisions using the same current harness; multiline output fingerprints intentionally differ because resource newlines are now protected with character references. Runtime-value preservation and real Git checkout behavior are verified separately by resource tests, since the old baseline does not preserve all resource newline values.

Suite version 3 adds preview and already formatted cases for 400 short multiline C# calls with `max_line_length = 120` and `csharp_keep_existing_linebreaks = false`, bringing the total to 69. These two cases establish their first fixed reference at `c277f78739fc11de2a563256f9f4d14699877f7c`, the reviewed implementation before batching. The existing 67 cases keep revision `09b320fd07321f65cd341d58b38f1d22be670c8b`; no reference is advanced and no tolerance is widened. Both compaction revisions must produce identical, stable output and preserve preview input bytes. The fixture exposes whole-document work repeated per call, which the long-list modes do not exercise.

The initial attempt to apply the older common reference to the new preview case measured 16.79 ms / 5.99 MiB for unchanged multiline behavior versus 32.60 ms / 11.22 MiB for batched compaction. That reference predates the feature and ignores `csharp_keep_existing_linebreaks`, so it measures different behavior. The separate pre-fix compaction reference corrects this comparison without replacing any established baseline. Initial-layout reuse further removes a duplicate format pass; its focused preview measured 27.22 ms / 7.97 MiB before the complete three-round validation. The same new fixture against the pre-fix implementation remains mandatory in every full validation.

## Initial baseline and optimization

### Short-call compaction

A same-machine Release comparison of the initial batched implementation against `c277f78`, with .NET 10.0.12 on Windows and tiered compilation disabled, measured these medians of nine individual previews after at least ten warm-up operations. The input contains independent multiline `F(1, 2)` calls inside one method, with a 120-column width and `csharp_keep_existing_linebreaks = false`:

| Calls | Before ms/file | Batched ms/file | Before MiB/file | Batched MiB/file |
| --- | ---: | ---: | ---: | ---: |
| 100 | 117.40 | 5.48 | 29.00 | 2.63 |
| 200 | 439.05 | 9.68 | 105.09 | 5.10 |
| 400 | 1,807.28 | 28.69 | 375.54 | 11.18 |

Batching removes repeated whole-document formatting per independent call and repeated token scans during width checks. Cleanup also skips reparsing when requested whitespace is already satisfied. These measurements demonstrate the allocation scaling improvement; they are not portable timing promises or a replacement for the mandatory pinned-baseline comparison. The pinned revision and tolerance values are unchanged.

After initial-layout reuse, complete three-round `Enforce` validation of `1364f8a` on VELA passed all 69 cases, coverage, packaging, and isolated installed-tool checks. The mandatory 400-call preview measured 1,664.60 to 17.32 ms and 381.93 to 8.02 MiB per file (97.9% lower allocations), with identical output fingerprints. Already formatted checks measured 8.82 to 7.74 ms. These results use the fixed pre-batching compaction reference; the other 67 cases retain their original reference and all tolerances remain unchanged.

### Repository dependency sorting

The first complete review-fix validation retained all allocation and correctness limits but failed three timing comparisons: 4,000 repeated metadata assignments (6.65/8.28 ms), 2,000 repeated properties (3.93/5.03 ms), and nested repository preview (58.45/72.09 ms). The sorting, file-read, and runner implementations for those paths were identical to the original pinned source; per-round timings drifted substantially (for example, the same current nested preview measured 103.51, 72.09, and 45.98 ms). Rather than changing references or tolerances, repeated single-name assignments now avoid building a dependency graph whose chain already fixes the entire order. The initial failure report remains retained for investigation.

Complete enforced validation after this optimization passed. Repeated metadata at 4,000 entries measured 5.195/4.850 ms and 7,970.8/7,063.3 KiB (baseline/current); repeated properties at 2,000 entries measured 3.619/3.218 ms and 5,141.4/4,687.6 KiB. Nested repository preview measured 53.032/47.291 ms. Allocation reductions are the direct effect of avoiding the redundant graph; the retained round-level timings document the machine variability.

### XML and XAML wrapping

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
