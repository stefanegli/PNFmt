# Formatter architecture

PNFmt processes files independently. `IFileFormatter` is the shared interface for the eight formatter adapters; it accepts a `FileFormatRequest` and returns status and formatter diagnostics in `FileFormatResult`.

## File processing

`TextFileFormatPipeline` owns input decoding, change detection, preview behavior, and writing for every formatter. `EncodedTextFile` retains the original bytes alongside strictly decoded text, so comparison uses the same input that was formatted. XML declarations identify input encoding independently of the requested output charset.

Document formatting returns `DocumentFormatResult`: formatted text, bytes from a format-specific serializer, or a skipped result with diagnostics. A skipped result prevents encoding changes as well as content changes. Encoding completes before writing, including during preview, so failures leave the original file intact.

Project, resource, solution, XML, and XAML implementations retain their own parsing, preservation, and serialization rules. In particular, legacy resource settings can leave an otherwise unchanged document untouched; each formatter retains its documented BOM and final-newline behavior.

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

`TargetFileResolver`, `GitRepositoryContext`, `FormattingRunner`, and MSBuild original-order dependency sorting keep their existing seams. They already concentrate substantive behavior and have tests through those interfaces.
