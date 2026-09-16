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

Setup, restoring inputs before each write, and correctness checks are outside the measured interval. Every warm-up and sample verifies complete discovery or expected statuses, no errors/diagnostics, unchanged preview bytes, identical serial/parallel output, and no leftover staging files. An incorrect result exits with failure; elapsed time has no pass/fail threshold. CI runs `--quick` on Windows and Linux.

Output includes runtime, OS, architecture, logical CPU count, worker count, minimum and median elapsed milliseconds, and median managed allocations in MiB. Allocation uses `GC.GetTotalAllocatedBytes` so worker-thread allocations are included; it also includes any unrelated managed background activity. This differs from the old benchmark's minimum allocation on the calling thread. Compare like-for-like runs, on the same machine, in Release, without other heavy work running.

These are warm-process and warm-cache measurements; they exclude CLI startup, console reporting, Git filtering, and formatting the EditorConfig files themselves. The nested case measures hierarchy lookup against the normal shared parsed-file cache, not cold parsing on every file. Timing depends on filesystem, antivirus, and storage flush latency; the full suite can take several minutes. Temporary fixtures are removed after completion or a handled failure.
