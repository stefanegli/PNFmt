# Snapshot review

From the repository root, generate a standalone HTML page:

```powershell
dotnet run --project tools/SnapshotViewer/PNFmt.SnapshotViewer.csproj --configuration Release
```

Open `artifacts/snapshot-review.html` in a browser. Select a file provider, search for a test, and compare the input with its saved snapshot result. The page includes changed-line highlighting, whitespace markers, final-newline information, and fixture configuration. It works offline without a server or external dependencies.

The exporter reads fixture files and the test's existing inline case data. It includes legacy RESX cases and default-configuration generation, and fails if a saved snapshot cannot be mapped to an input. Tests without saved snapshots are not part of this viewer.

Results come from the current files under `Snapshots/`; they are not regenerated or approved by the viewer. Run the snapshot tests first if you want fresh formatter output, then regenerate the page. Snapshot tests may update those files, so review their Git diff before approving them.

## Wrapping review cases

Search for `WrappingReview` to inspect the C#, XML, and XAML cases added after the
[CSharpier comparison](../../docs/reviews/csharpier-comparison.html). Their fixture
configuration explicitly enables layout and an 80-column width with four-space
indentation; XML/XAML also enables attribute wrapping. The two attribute-arrangement
cases select their styles by filename. Optional C# sorting and cleanup remain off.

The saved outputs describe current behavior, including limitations, rather than a
proposed target style. Compare the supported call and structural-attribute cases
with collection/array initializers, tuples, ternaries, patterns, queries, switch
expressions, and text-bearing XML/XAML. The short single-line and multiline call
cases show how existing line breaks affect the result. Cases with identical input
and output are intentional: they make preserved layouts and unwrapped constructs
visible in the review. Update these baselines deliberately if formatting policy
changes later.

The existing snapshot harness checks preview behavior and second-run stability for
each case; no separate test runner or formatter implementation is involved.

Pass an optional output path after `--` to export elsewhere. The generated page embeds all input and result text; regenerate it whenever the fixtures or snapshots change.

The .NET test suite checks catalog coverage and export safety. If Node.js is available, run `node --test tools/SnapshotViewer/viewer.test.cjs` after exporting to check line alignment, filtering, navigation, and display controls. These script tests do not replace a visual browser review.
