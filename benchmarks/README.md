Run the dependency-sorting allocation benchmark with:

```powershell
dotnet run --project benchmarks/PNFmt.Benchmarks.csproj --configuration Release
```

It formats temporary SDK projects containing repeated, self-referencing property or metadata assignments. Each case warms up once and reports the minimum elapsed time and thread allocation from three dry runs. The benchmark uses the public formatter, including parsing, settings lookup, dependency ordering, and serialization.

Compare allocation growth as the entry count doubles. Timing is informational and has no pass/fail threshold; correctness is covered by the MSBuild evaluation and dependency-order tests. Temporary files are removed after the run.
