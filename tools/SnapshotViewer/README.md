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

Processed cases with a saved earlier result default to comparing the
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

Queries use `csharp_wrap_linq_expressions = chop_if_long` and
`csharp_wrap_before_linq_expression = true`. Switch expressions use
`csharp_wrap_switch_expression = chop_if_long`; tuples use the PNFmt-specific
`pnfmt_csharp_wrap_tuple_style = chop_if_long`. Short calls use
`csharp_keep_existing_linebreaks = false` alongside
`csharp_wrap_arguments_style = chop_if_long`. Their multiline and single-line inputs
now produce the same result. Additional cases cover short expressions, query
continuations, nested calls/switches, and tuple types.

XML/XAML text-bearing and mixed-content start tags now use the same wrapping settings
as structural elements, while preserving all text separators and CDATA. The four
original XML/XAML decision cases are processed and retain their previous results for
comparison. `TagSpelling.xml` selects single-line attribute style and the existing
spacing settings (no spaces around `=` or before `>`, one space before `/>`).
`TagSpacingExpanded.xml` shows the opposite spacing choices, and
`InlineTextAndCData.xml` shows wrapping throughout nested inline markup.

The saved outputs describe current behavior, including limitations, rather than a
proposed target style. Quote normalization is the only deferred decision, shown in
`QuoteStyle.xml`. No quote setting is implemented; all quotes retain their spelling.
All other cases have been processed or are reference examples.
Processed cases still await the user's review. Cases with identical input
and output are intentional: they make preserved layouts and unwrapped constructs
visible in the review. Update these baselines deliberately if formatting policy
changes later.

The existing snapshot harness checks preview behavior and second-run stability for
each case; no separate test runner or formatter implementation is involved.

Pass an optional output path after `--` to export elsewhere. The generated page embeds all input and result text; regenerate it whenever the fixtures or snapshots change.

The .NET test suite checks catalog coverage and export safety. If Node.js is available, run `node --test tools/SnapshotViewer/viewer.test.cjs` after exporting to check line alignment, filtering, navigation, and display controls. These script tests do not replace a visual browser review.
