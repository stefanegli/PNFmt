# Formatter test audit — 2026-09-25

This audit covers every xUnit test in `PNFmt.Tests`: all formatters, shared
configuration/encoding/file-write contracts, CLI behavior and test infrastructure.
It expands the preceding focused RESX cleanup into a complete declaration review.
Baseline: `37bee22ba15f2210d22b4aa29d73bde5fb3532fa` (2,109 passing cases).

All 52 test files and 436 declarations were read, including parameter tables,
production callers, overlapping keepers, fixture inputs and relevant history.
Dependency-backed contracts were checked against installed API documentation or
source. Each declaration received one decision before changes: 395 retain (R),
12 repair (F), 12 consolidate (C), 17 delete (D). Consolidation marks include
keepers that absorb proof; they are not a count of deleted tests.

## Scope and inventory

The table records baseline declarations, cases and decisions, not coverage
percentages. All listed files passed the baseline. Every declaration belongs to
exactly one lane; no complete test file was retired.

| Test file | Lane | Declarations | Cases | Decisions |
| --- | --- | ---: | ---: | --- |
| [CommandLineOptionsTests.cs](../PNFmt.Tests/CommandLineOptionsTests.cs) | cli | 7 | 20 | 7R |
| [FilePatternMatcherTests.cs](../PNFmt.Tests/FilePatternMatcherTests.cs) | cli | 3 | 10 | 3R |
| [Formatter/CharsetTests.cs](../PNFmt.Tests/Formatter/CharsetTests.cs) | shared | 15 | 189 | 14R, 1C |
| [Formatter/CSharp/CSharpCleanupTests.cs](../PNFmt.Tests/Formatter/CSharp/CSharpCleanupTests.cs) | csharp | 11 | 24 | 11R |
| [Formatter/CSharp/CSharpExclusionTests.cs](../PNFmt.Tests/Formatter/CSharp/CSharpExclusionTests.cs) | csharp | 6 | 6 | 6R |
| [Formatter/CSharp/CSharpFileHeaderTests.cs](../PNFmt.Tests/Formatter/CSharp/CSharpFileHeaderTests.cs) | csharp | 16 | 47 | 16R |
| [Formatter/CSharp/CSharpFormatterTests.cs](../PNFmt.Tests/Formatter/CSharp/CSharpFormatterTests.cs) | csharp | 15 | 30 | 13R, 1F, 1C |
| [Formatter/CSharp/CSharpLineWrappingTests.cs](../PNFmt.Tests/Formatter/CSharp/CSharpLineWrappingTests.cs) | csharp | 25 | 59 | 24R, 1F |
| [Formatter/CSharp/CSharpListWrappingTests.cs](../PNFmt.Tests/Formatter/CSharp/CSharpListWrappingTests.cs) | csharp | 20 | 53 | 20R |
| [Formatter/CSharp/CSharpMemberSorterTests.cs](../PNFmt.Tests/Formatter/CSharp/CSharpMemberSorterTests.cs) | csharp | 20 | 38 | 20R |
| [Formatter/CSharp/CSharpNewLinePreferencesTests.cs](../PNFmt.Tests/Formatter/CSharp/CSharpNewLinePreferencesTests.cs) | csharp | 12 | 38 | 12R |
| [Formatter/CSharp/CSharpRegionRemoverTests.cs](../PNFmt.Tests/Formatter/CSharp/CSharpRegionRemoverTests.cs) | csharp | 14 | 26 | 13R, 1C |
| [Formatter/CSharp/CSharpSnapshotTests.cs](../PNFmt.Tests/Formatter/CSharp/CSharpSnapshotTests.cs) | csharp | 1 | 10 | 1R |
| [Formatter/CSharp/CSharpUsingSorterTests.cs](../PNFmt.Tests/Formatter/CSharp/CSharpUsingSorterTests.cs) | csharp | 10 | 10 | 9R, 1F |
| [Formatter/CsProj/FinalNewlineTests.cs](../PNFmt.Tests/Formatter/CsProj/FinalNewlineTests.cs) | msbuild | 1 | 9 | 1R |
| [Formatter/CsProj/FormattingTests.cs](../PNFmt.Tests/Formatter/CsProj/FormattingTests.cs) | msbuild | 1 | 22 | 1R |
| [Formatter/CsProj/ItemCanonicalizerTests.cs](../PNFmt.Tests/Formatter/CsProj/ItemCanonicalizerTests.cs) | msbuild | 2 | 2 | 2R |
| [Formatter/CsProj/LintingTests.cs](../PNFmt.Tests/Formatter/CsProj/LintingTests.cs) | msbuild | 5 | 20 | 5R |
| [Formatter/CsProj/MsBuildEvaluationTests.cs](../PNFmt.Tests/Formatter/CsProj/MsBuildEvaluationTests.cs) | msbuild | 10 | 31 | 10R |
| [Formatter/CsProj/NonSdkProjectTests.cs](../PNFmt.Tests/Formatter/CsProj/NonSdkProjectTests.cs) | msbuild | 2 | 6 | 2R |
| [Formatter/CsProj/OriginalOrderDependenciesTests.cs](../PNFmt.Tests/Formatter/CsProj/OriginalOrderDependenciesTests.cs) | msbuild | 1 | 1 | 1R |
| [Formatter/EditorConfig/DefaultEditorConfigDocumentTests.cs](../PNFmt.Tests/Formatter/EditorConfig/DefaultEditorConfigDocumentTests.cs) | shared | 12 | 15 | 12R |
| [Formatter/EditorConfig/EditorConfigSettingsTests.cs](../PNFmt.Tests/Formatter/EditorConfig/EditorConfigSettingsTests.cs) | shared | 13 | 30 | 12R, 1D |
| [Formatter/EditorConfig/FormatterActivationTests.cs](../PNFmt.Tests/Formatter/EditorConfig/FormatterActivationTests.cs) | shared | 6 | 27 | 6R |
| [Formatter/EditorConfig/FormatterBehaviorTests.cs](../PNFmt.Tests/Formatter/EditorConfig/FormatterBehaviorTests.cs) | shared | 10 | 34 | 8R, 2F |
| [Formatter/EditorConfig/FormatterOptionMatrixTests.cs](../PNFmt.Tests/Formatter/EditorConfig/FormatterOptionMatrixTests.cs) | shared | 4 | 896 | 4R |
| [Formatter/EncodedTextFileTests.cs](../PNFmt.Tests/Formatter/EncodedTextFileTests.cs) | shared | 2 | 9 | 2R |
| [Formatter/FileReplacementTests.cs](../PNFmt.Tests/Formatter/FileReplacementTests.cs) | shared | 11 | 15 | 11R |
| [Formatter/FormatterContractTests.cs](../PNFmt.Tests/Formatter/FormatterContractTests.cs) | shared | 2 | 16 | 2R |
| [Formatter/Ini/EditorConfigPrecedenceTests.cs](../PNFmt.Tests/Formatter/Ini/EditorConfigPrecedenceTests.cs) | markup_text | 3 | 13 | 3R |
| [Formatter/Ini/IniFormatterTests.cs](../PNFmt.Tests/Formatter/Ini/IniFormatterTests.cs) | markup_text | 11 | 15 | 4R, 7D |
| [Formatter/Ini/IniSnapshotTests.cs](../PNFmt.Tests/Formatter/Ini/IniSnapshotTests.cs) | markup_text | 1 | 17 | 1R |
| [Formatter/Resx/AdversarialFormattingTests.cs](../PNFmt.Tests/Formatter/Resx/AdversarialFormattingTests.cs) | resources | 15 | 20 | 12R, 1F, 2D |
| [Formatter/Resx/EditorConfigTests.cs](../PNFmt.Tests/Formatter/Resx/EditorConfigTests.cs) | resources | 8 | 8 | 7C, 1D |
| [Formatter/Resx/FormattingTests.cs](../PNFmt.Tests/Formatter/Resx/FormattingTests.cs) | resources | 3 | 20 | 3R |
| [Formatter/Resx/ResxLayoutTests.cs](../PNFmt.Tests/Formatter/Resx/ResxLayoutTests.cs) | resources | 9 | 29 | 9R |
| [Formatter/Rsp/RspSnapshotTests.cs](../PNFmt.Tests/Formatter/Rsp/RspSnapshotTests.cs) | markup_text | 1 | 10 | 1R |
| [Formatter/Slnx/SlnxFormatterTests.cs](../PNFmt.Tests/Formatter/Slnx/SlnxFormatterTests.cs) | markup_text | 9 | 19 | 7R, 2D |
| [Formatter/Slnx/SlnxSnapshotTests.cs](../PNFmt.Tests/Formatter/Slnx/SlnxSnapshotTests.cs) | markup_text | 2 | 11 | 2R |
| [Formatter/Xml/XamlFormatterTests.cs](../PNFmt.Tests/Formatter/Xml/XamlFormatterTests.cs) | markup_text | 6 | 15 | 4R, 2D |
| [Formatter/Xml/XmlAttributeWrappingTests.cs](../PNFmt.Tests/Formatter/Xml/XmlAttributeWrappingTests.cs) | markup_text | 24 | 60 | 24R |
| [Formatter/Xml/XmlFormatterTests.cs](../PNFmt.Tests/Formatter/Xml/XmlFormatterTests.cs) | markup_text | 10 | 26 | 7R, 1F, 2D |
| [Formatter/Xml/XmlSnapshotTests.cs](../PNFmt.Tests/Formatter/Xml/XmlSnapshotTests.cs) | markup_text | 2 | 18 | 2R |
| [FormatterRegistryTests.cs](../PNFmt.Tests/FormatterRegistryTests.cs) | cli | 3 | 3 | 3R |
| [FormattingReporterTests.cs](../PNFmt.Tests/FormattingReporterTests.cs) | cli | 3 | 10 | 3R |
| [FormattingRunnerTests.cs](../PNFmt.Tests/FormattingRunnerTests.cs) | cli | 4 | 4 | 4R |
| [PathSelectionTests.cs](../PNFmt.Tests/PathSelectionTests.cs) | cli | 3 | 5 | 3R |
| [ProgramTests.cs](../PNFmt.Tests/ProgramTests.cs) | cli | 49 | 88 | 42R, 5F, 2C |
| [Snapshots/CliFormatterIntegrationTests.cs](../PNFmt.Tests/Snapshots/CliFormatterIntegrationTests.cs) | harness | 2 | 14 | 2R |
| [Snapshots/GitSnapshotTests.cs](../PNFmt.Tests/Snapshots/GitSnapshotTests.cs) | harness | 8 | 8 | 8R |
| [Snapshots/SnapshotReviewExporterTests.cs](../PNFmt.Tests/Snapshots/SnapshotReviewExporterTests.cs) | harness | 2 | 2 | 2R |
| [TestProcessTests.cs](../PNFmt.Tests/TestProcessTests.cs) | harness | 1 | 1 | 1R |

The dedicated JavaScript snapshot viewer and PowerShell validation-script test
suites are outside this xUnit audit. Their implementations were not pruned.
Windows execution does not prove Unix-only branches; those remain Linux CI
contracts. No baseline product defect was found.

## Removed and consolidated layers

| Removed declaration | Remaining owner / reason |
| --- | --- |
| `IniFormatterTests.Comments_unknown_lines_and_sections_are_sort_barriers` | IniSnapshotTests.Formatter_matches_snapshot: Barriers.ini, Sections.ini |
| `IniFormatterTests.Dry_run_detects_changes_and_a_second_run_is_unchanged` | IniSnapshotTests.Formatter_matches_snapshot + FormatterSnapshotTestRunner.FormatAndAssertIdempotent |
| `IniFormatterTests.Group_merging_activates_formatting` | IniSnapshotTests.Formatter_matches_snapshot: MergingOnly/RepeatedSections.ini |
| `IniFormatterTests.Prefix_grouping_activates_formatting_and_sorts_the_property_block` | IniSnapshotTests.Formatter_matches_snapshot: PrefixOnly/Groups.ini |
| `IniFormatterTests.Sorts_contiguous_properties_and_normalizes_spacing` | IniSnapshotTests.Formatter_matches_snapshot: EditorConfig/.editorconfig, Whitespace.ini, Barriers.ini |
| `IniFormatterTests.Sorts_group_contents_with_their_section_header` | IniSnapshotTests.Formatter_matches_snapshot: GroupSortingOnly/Sections.ini |
| `IniFormatterTests.Sorts_groups_without_sorting_entries_when_only_group_sorting_is_enabled` | IniSnapshotTests.Formatter_matches_snapshot: GroupSortingOnly/Sections.ini |
| `SlnxFormatterTests.Dry_run_detects_changes_and_a_second_run_is_unchanged` | SlnxSnapshotTests.Formatter_matches_snapshot + FormatterSnapshotTestRunner.FormatAndAssertIdempotent |
| `SlnxFormatterTests.Unknown_extension_elements_are_sort_barriers` | SlnxSnapshotTests.Formatter_matches_snapshot: ExtensionBarrier.slnx |
| `XamlFormatterTests.Custom_containers_keep_their_own_whitespace_while_known_descendants_can_format` | XmlSnapshotTests.Formatter_matches_snapshot: CustomContainers.xaml |
| `XamlFormatterTests.Formats_layout_and_property_elements_without_loading_types` | XmlSnapshotTests.Formatter_matches_snapshot: View.xaml, Layout/Tabs.xaml |
| `XmlFormatterTests.Excessive_nesting_is_rejected_before_recursive_rendering` | XmlSnapshotTests.Rejected_files_remain_unchanged: Rejected/ExcessiveNesting.xml; Formatter_matches_snapshot: Boundary/MaximumNesting.xml |
| `XmlFormatterTests.Indents_elements_without_reordering_or_reserializing_markup` | XmlSnapshotTests.Formatter_matches_snapshot: Data.xml, MarkupAndProcessingInstructions.xml |
| `AdversarialFormattingTests.Constructor_rejects_missing_settings_at_the_boundary` | ResxFormatter.Format and FileFormattingConfiguration.ResourceSettings enforce real construction path; no production null-settings contract |
| `AdversarialFormattingTests.Removing_documentation_preserves_unrelated_comments` | AdversarialFormattingTests.Entry_comments_and_file_footers_keep_their_association_during_rewrites; Documentation_is_found_and_removed_even_after_an_unrelated_comment; FormatterOptionMatrixTests.Resx_switches_follow_every_boolean_combination_with_content_present_or_absent |
| `EditorConfigTests.Dry_run_detects_changes_without_writing_the_file` | ResxLayoutTests.Adding_generated_documentation_is_idempotent_with_layout_or_encoding_overrides; ResxSnapshotTests.Formatter_matches_snapshot |
| `EditorConfigSettingsTests.Cached_editorconfig_files_refresh_after_changes` | EditorConfigSettingsTests.Loads_observe_added_changed_and_removed_child_configurations; FormatterActivationTests.Configuration_is_consistent_during_formatting_and_refreshes_when_a_request_is_reused |

- The C# malformed UTF-8 replay moved into shared `CharsetTests`. Its exact
  `DecoderFallbackException` requirement now applies to non-XML rows, while XML
  rows retain their decoder/XML exception alternatives. Preview/write and all
  omitted/unset/invalid charset cases remain.
- One repeated region-remover assertion was removed. All three protected
  endpoint rows still assert exact pipeline output, no diagnostic and idempotence;
  the raw-removal helper retains its other independent contracts.
- CLI dry-run summary proof moved into the existing byte-preserving BOM test.
  Configuration rejection now has one command-boundary table with 13 unique
  inputs, correct error categories, exit code 2 and the exact `.pnfmt` path.
- Seven RESX configuration snapshot methods became one seven-row theory. All
  eight original snapshot identities remain. The sibling configurations still
  share one formatter in one invocation; the whole configuration tree is copied
  to a scoped temporary directory and culture is restored in `finally`.
- The INI path constructor had only the deleted cache test as a caller. Production
  uses its resolved-configuration constructor. The internal RESX constructor null
  guard was likewise exercised only by tests; its sole production caller always
  supplies settings. These changes alter no public formatter contract.

## Assertion repairs and mutation proof

Twelve retained declarations had incomplete assertions. Ordering checks now
require both entries or compare complete parsed sequences. C# method identifiers
are exact: the former `void A` check could match `void Anchor`. The XML/XAML
encoding test uses an independent literal containing the original Unicode text.
The dense RESX sort checks all 4,096 names instead of only count and endpoints.

Temporary mutations were applied to production owners with narrow filters that
excluded snapshot writers. The original assertions accepted every mutated case;
the repaired assertions failed for the intended missing/misordered content.

| Mutation group | Original tests | Repaired tests |
| --- | --- | --- |
| SLNX first-entry loss; C# method/field renaming; XML Unicode loss; RESX middle swap | 11 pass | 8 fail; 3 unaffected rows pass |
| CLI project Alpha loss, or Zeta loss when sorting is disabled | 4 pass | 4 fail |
| CLI resource `a` entry loss | 1 pass | 1 fail |
| CLI solution `A.csproj` loss | 1 pass | 1 fail |

The recursive CLI case appears in two distinct mutation groups. All source and
test files were restored byte for byte after each group. These controls demonstrate
test weaknesses, not existing product defects. Independent preservation reviewers
checked markup/text, shared/C#, and RESX/CLI boundaries. No deleted contract lost
its keeper. Review identified the Anchor-prefix ambiguity before cutover and
tightened the new configuration error assertion to the complete target path.

## Valuable overlaps retained

- Every MSBuild declaration remains: approved text and real evaluated project
  semantics protect different contracts. Dependency ordering, lint locations,
  non-SDK compatibility and complete final-newline bytes remain independent.
- C# compiled initializer behavior, exclusion restoration across passes, generated
  source policy, wrapping defaults and six original encoding cases remain.
- XML/XAML mixed text, physical CR/CRLF, inline containers, namespace rules and
  attribute wrapping retain dedicated tests; normalized snapshots cannot replace
  those byte or option-boundary contracts.
- INI precedence uses the actual EditorConfig parser and explicit expected
  effective values. RSP and SLNX physical newline/extension preservation remain.
- Shared atomic replacement, concurrency, permissions, configuration freshness,
  default generation and migration remain. CLI transport/reporting and the real
  subprocess/snapshot approval harness have risks distinct from rendering.

## Size and validation

| Measure | Baseline | Final |
| --- | ---: | ---: |
| Test files | 52 | 52 |
| Test declarations | 436 | 410 |
| Executed xUnit cases | 2,109 | 2,089 |
| Lines in test files (including embedded support) | 9,888 | 9,367 |
| Standalone test-support lines | 740 | 740 |

Production changed by +1/-6 lines (net -5); test files by +92/-613 (net -521).
Fixtures and snapshots are unchanged. Test ownership guidance is recorded in
[PNFmt.Tests/AGENTS.md](../PNFmt.Tests/AGENTS.md).

Validation on VELA passed:

- Focused formatter/CLI suite: 2,015 cases.
- Repository formatting and `-Check -NoBuild`; `git diff --check`.
- Complete `Publish-GlobalTool.ps1 -Version 0.0.0-validation.1 -PackOnly` (the
  validation entry point at the audited revision), with no
  skip switches and the default `TimingPolicy Enforce`: 2,089 Release cases,
  coverage gates, 35 performance-gate regression checks, all 59 measured cases
  with zero timing warnings, packaging and isolated installed-tool checks.
- Core coverage: 97.57% line / 91.96% branch. CLI: 93.37% / 84.19%.
  Thresholds, timing tolerances and the pinned performance baseline are unchanged.

Coverage evidence is in `artifacts/coverage/1794fa8d7086435382ff13b6f66ed541/`;
performance evidence is in `artifacts/performance/3b57335806054fc1bbda5772e1e7d7e9/`.

Local detailed evidence is retained under `artifacts/test-audit-remaining/`:
the baseline inventory, one declaration ledger and layer plan per lane, independent
preservation reports, mutation TRX/logs and complete validation log. These generated
artifacts are intentionally ignored by Git; this report records the durable scope,
ownership decisions and results.
