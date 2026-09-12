# Snapshot review

From the repository root, generate a standalone HTML page:

```powershell
dotnet run --project tools/SnapshotViewer/PNFmt.SnapshotViewer.csproj --configuration Release
```

Open `artifacts/snapshot-review.html` in a browser. Select a file provider, search for a test, and compare the input with its saved snapshot result. The page includes changed-line highlighting, whitespace markers, final-newline information, and fixture configuration. It works offline without a server or external dependencies.

The exporter reads fixture files and the test's existing inline case data. It includes legacy RESX cases and default-configuration generation, and fails if a saved snapshot cannot be mapped to an input. Tests without saved snapshots are not part of this viewer.

Results come from the current files under `Snapshots/`; they are not regenerated or approved by the viewer. Run the snapshot tests first if you want fresh formatter output, then regenerate the page. Snapshot tests may update those files, so review their Git diff before approving them.

Pass an optional output path after `--` to export elsewhere. The generated page embeds all input and result text; regenerate it whenever the fixtures or snapshots change.

The .NET test suite checks catalog coverage and export safety. If Node.js is available, run `node --test tools/SnapshotViewer/viewer.test.cjs` after exporting to check line alignment, filtering, navigation, and display controls. These script tests do not replace a visual browser review.
