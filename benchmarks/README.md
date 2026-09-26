Run the full benchmark suite with:

```powershell
dotnet run --project benchmarks/PNFmt.Benchmarks.csproj --configuration Release
```

For a short validation run (64 files per repository, 500 sorting entries, one measured sample):

```powershell
dotnet run --project benchmarks/PNFmt.Benchmarks.csproj --configuration Release -- --quick
```

The full suite uses three measured samples after one warm-up per case:

| Scenario | Input |
| --- | --- |
| Properties / metadata | One SDK project containing 500, 1,000, 2,000, or 4,000 self-referencing assignments |
| Small projects | 2,048 small SDK projects with unsorted items |
| Mixed flat | 2,048 files split equally across C#, project, resource, INI, response, solution, XML, and XAML formats |
| Mixed nested | The same format mix under 16 branches, each with four levels of inherited EditorConfig overrides |

Repository cases use the CLI's actual `TargetFileResolver` and `FormattingRunner`. Discovery is measured separately; execution uses the discovered paths. Each repository runs serially and with up to eight workers (at least two, even on a single-CPU host). Execution phases measure preview of unformatted files, writing changed files, and checking already formatted files. Writes include permission-preserving staging, flushing, conflict detection, replacement, and cleanup.

Setup, restoring inputs before each write, and correctness checks are outside the measured interval. Every warm-up and sample verifies complete discovery or expected statuses, no errors/diagnostics, unchanged preview bytes, identical serial/parallel output, and no leftover staging files. An incorrect result exits with failure. These standalone full/quick runs do not impose timing thresholds. CI also runs the separate publish performance gate described below.

Output includes runtime, OS, architecture, logical CPU count, worker count, minimum and median elapsed milliseconds, and median managed allocations in MiB. Allocation uses `GC.GetTotalAllocatedBytes` so worker-thread allocations are included; it also includes any unrelated managed background activity. This differs from the old benchmark's minimum allocation on the calling thread. Compare like-for-like runs, on the same machine, in Release, without other heavy work running.

These are warm-process and warm-cache measurements; they exclude CLI startup, console reporting, Git filtering, and formatting the EditorConfig files themselves. The nested case measures hierarchy lookup against the normal shared parsed-file cache, not cold parsing on every file. Timing depends on filesystem, antivirus, and storage flush latency; the full suite can take several minutes. Temporary fixtures are removed after completion or a handled failure.

## Publish performance gate

```powershell
./scripts/Test-Performance.ps1
```

The gate compiles this checkout's harness against both the current formatter/CLI and the revision pinned in [performance-baseline.json](performance-baseline.json). It runs 67 cases in isolated processes, three times per revision, and checks each case's elapsed time and total managed allocations. The repository scenarios use 128 files and seven samples, batching read-only operations after at least three operations/300 ms of warm-up. Formatter scenarios use 1,000 XML/XAML/MSBuild elements, 1,000 RESX entries, or 200 C# methods and nine batched samples after warm-up. Both initial previews and already formatted checks are included. C# modes cover defaults, width, argument/parameter list styles, Microsoft blank-line preferences, and headers; XML modes cover defaults, width, and one attribute per line. RESX modes cover LF and CRLF layout with single-line values, CRLF with multiline values, and CRLF with longer multiline values. These cases exercise the CRLF path that the existing LF-configured mixed-repository fixtures do not reach.

`Program --performance <family> <report.json>` is the worker entry point (`xml`, `xaml`, `csproj`, `csharp`, `resx`, or `repository`). It writes samples and environment metadata; the script controls builds, process isolation, runtime settings, and revision order. `FormatterSourceRoot` selects the referenced source tree when building the same harness for both revisions. Run the script for a valid gate result rather than invoking a worker alone.

See [performance gates](../docs/performance.md) for tolerance rules, report locations, CI visibility, and how to review intentional baseline changes.

The script defaults to `-TimingPolicy Enforce`, which fails timing or allocation regressions. Hosted workflows explicitly select `-TimingPolicy ReportOnly`: all measurements still run and timing regressions produce warnings, while allocations, correctness, report validity, and output stability remain enforced. Complete release validation on the controlled local machine, VELA, must use the default enforcement mode before a release tag is pushed. The reports record the timing policy and machine name.
