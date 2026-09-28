# Snapshot review

From the repository root, generate a standalone HTML page:

```powershell
dotnet run --project tools/SnapshotViewer/PNFmt.SnapshotViewer.csproj --configuration Release
```

Open `artifacts/snapshot-review.html` in a browser. Select a file provider, search for a test, and compare the input with its saved snapshot result. The page includes changed-line highlighting, whitespace markers, final-newline information, and fixture configuration. It works offline without a server or external dependencies.

The exporter reads fixture files and the test's existing inline case data. It includes legacy RESX cases and default-configuration generation, and fails if a saved snapshot cannot be mapped to an input. Tests without saved snapshots are not part of this viewer.

Results come from the current files under `Snapshots/`; they are not regenerated or approved by the viewer. Run the snapshot tests first if you want fresh formatter output, then regenerate the page. Snapshot tests may update those files, so review their Git diff before approving them.

## Wrapping review cases

Generate the focused decision review with:

```powershell
dotnet run --project tools/SnapshotViewer/PNFmt.SnapshotViewer.csproj --configuration Release -- --wrapping-review
```

Open `artifacts/wrapping-review.html`. The status buttons separate **Processed**
(implemented and ready for your review), **Needs decision** (open formatting
questions), and **Reference** (previously supported behavior). These statuses are
explicit review notes, not inferred from whether the input differs from its result.
Processed does not mean the user has approved the output.

The array-initializer, collection-expression, conditional-expression, and property-pattern cases default to comparing the
previous snapshot with the current snapshot. Use **Compare with** to switch back
to the fixture input. New cases compare their fixture input with the saved result.
The previous snapshots and the questions/statuses are retained in
`wrapping-review.json`; update that manifest deliberately as decisions are made.
Every `WrappingReview/` snapshot must have an entry, and stale entries fail export.

Search for `WrappingReview` to inspect the C#, XML, and XAML cases added after the
[CSharpier comparison](../../docs/reviews/csharpier-comparison.html). Their fixture
configuration explicitly enables layout and an 80-column width with four-space
indentation; XML/XAML also enables attribute wrapping. The two attribute-arrangement
cases select their styles by filename. C# explicitly selects `chop_always` for
array initializers, object/collection initializers, and collection expressions/list
patterns. Ternaries use `csharp_wrap_ternary_expr_style = chop_if_long` and
`csharp_wrap_before_ternary_opsigns = true`; short and nested examples make the
width-dependent behavior visible. Optional C# sorting and cleanup remain off.

Property patterns use `csharp_wrap_property_pattern = chop_if_long`. The processed
`Patterns.cs` comparison retains the actual previous result, including its wrapped
method signature. `ShortPattern.cs` shows a compact pattern, and `NestedPatterns.cs`
shows short inner patterns staying compact while long ones expand.

The saved outputs describe current behavior, including limitations, rather than a
proposed target style. Review the processed collection/initializer/list-pattern/property-pattern and ternary
cases, then compare the supported call and structural-attribute cases with tuples,
queries, switch expressions, and text-bearing
XML/XAML that still need decisions. The short single-line and multiline call
cases show how existing line breaks affect the result. Cases with identical input
and output are intentional: they make preserved layouts and unwrapped constructs
visible in the review. Update these baselines deliberately if formatting policy
changes later.

The existing snapshot harness checks preview behavior and second-run stability for
each case; no separate test runner or formatter implementation is involved.

Pass an optional output path after `--` to export elsewhere. The generated page embeds all input and result text; regenerate it whenever the fixtures or snapshots change.

The .NET test suite checks catalog coverage and export safety. If Node.js is available, run `node --test tools/SnapshotViewer/viewer.test.cjs` after exporting to check line alignment, filtering, navigation, and display controls. These script tests do not replace a visual browser review.
