# Formatter architecture

PNFmt processes files independently. `IFileFormatter` is the shared interface for the eight formatter adapters; it accepts a `FileFormatRequest` and returns status and formatter diagnostics in `FileFormatResult`.

## File processing

`TextFileFormatPipeline` owns input decoding, change detection, preview behavior, and writing for every formatter. `EncodedTextFile` retains the original bytes alongside strictly decoded text, so comparison uses the same input that was formatted. XML declarations identify input encoding independently of the requested output charset.

Document formatting returns `DocumentFormatResult`: formatted text, bytes from a format-specific serializer, or a skipped result with diagnostics. A skipped result prevents encoding changes as well as content changes. Encoding completes before writing, including during preview, so failures leave the original file intact.

`FileReplacement` stages output in a unique sibling file, retains copied file permissions, and flushes it before replacement. Immediately before replacing, it compares the source against the bytes that were formatted and refuses intervening edits. It requires source write permission and rejects symbolic links/reparse points; format their targets directly. Preview and unchanged files need no write access or temporary files.

Replacement uses a backup for recovery if the file system fails partway through; normal completion removes staging and backup files. Abrupt process termination can leave `.pnfmt-*.tmp` or `.tmp.bak` siblings. This is an optimistic edit check, not a transaction with external editors: path replacement can still race the final check, and durability depends on the file system. Replacing a path gives it a new file identity, so other hard links retain the previous contents. Windows replacement preserves destination security metadata; Unix permission bits are copied, but ownership and extended metadata depend on the runtime/file system.

Project, resource, solution, XML, and XAML implementations retain their own parsing, preservation, and serialization rules. In particular, legacy resource settings can leave an otherwise unchanged document untouched; each formatter retains its documented BOM and final-newline behavior.

Project and resource document formatters return `FileFormatResult` directly from each call. They retain settings, but no per-run status, diagnostics, or logger; callers keep independent results and supply operation context through `FileFormatRequest`.

XML and XAML attribute wrapping shares the normal layout render, using validated source slices and cached indentation rather than parsing an intermediate formatted document. XML declaration encoding is normalized before width measurement; final XML validation and protected-subtree rules remain in place. MSBuild applies the shared attribute writer after its existing project serializer.

## Configuration

`FileFormattingConfiguration` owns selection, activation, compatibility rules, layout enablement, and charset resolution. Format-specific settings are resolved from the same EditorConfig hierarchy, on demand.

Discovery can resolve configuration to select candidate files. Execution resolves it again, after earlier `.editorconfig` files have been processed. `FormattingRunner` shares that execution configuration with dispatch and the formatter. Direct formatter calls follow the same path through `FileFormatRequest.ResolveConfiguration`, which creates a resolved copy so callers can reuse their original request and observe later configuration changes.

The EditorConfig parsed-file cache remains shared; a resolved hierarchy is scoped to one file operation. Generated configuration defaults are maintained separately because they intentionally enable behaviors that default off when omitted.

## Reporting

`FormattingRunner` buffers per-file messages, exceptions, and results while preserving input order across concurrent work. `FormatterLogMessage` distinguishes compatibility warnings, progress, and detail without inspecting message wording. Existing `IFormatterLog` adapters continue to receive text; the CLI adapter retains structured messages until reporting.

`FormattingReporter` owns output streams, verbose detail, statuses, summaries, and formatting-run exit codes. Compatibility warnings remain visible without verbosity and do not fail checks. Formatter diagnostics affect the exit code in lint mode. Path and formatting errors take precedence and return exit code 2.

## Verification

- Charset and snapshot tests exercise preview, exact bytes, rejected input, and repeated formatting through the file formatter interface.
- Activation and option matrices exercise the configuration contract; hierarchy-refresh and request-reuse tests cover configuration lifetime.
- MSBuild evaluation and XML preservation tests protect format-specific semantics.
- Reporting tests use captured text writers; CLI integration tests verify dispatch, output, and exit codes together.
- File replacement tests cover partial staging failures, intervening edits, read-only files, cleanup, and platform-specific permission/link behavior.

The [benchmark suite](../benchmarks/README.md) measures dependency sorting and repository discovery/execution, including mixed formats, nested configuration, serial/parallel work, and staged writes. Its short CI run checks outcomes and byte stability without timing thresholds.

`TargetFileResolver`, `GitRepositoryContext`, `FormattingRunner`, and MSBuild original-order dependency sorting keep their existing seams. They already concentrate substantive behavior and have tests through those interfaces.
