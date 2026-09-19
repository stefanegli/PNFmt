# Other formatters

Use the section for the selected formatter. Shared activation, encoding, final-newline rules, and CLI behavior are in [SKILL.md](../SKILL.md).

## MSBuild project files (`csproj`)

Both SDK-style and non-SDK documents with a `Project` root are supported, including the legacy MSBuild XML namespace. An `Sdk` declaration is not required. Check contents rather than inferring project style from target framework or extension. Select `pnfmt_formatter = csproj` explicitly for `.props`, `.targets`, and `.proj`.

- `pnfmt_sort_entries = true` sorts eligible project-level properties/items and canonicalizes eligible items while preserving evaluation-sensitive content.
- `pnfmt_csproj_empty_lines_between_groups` is a non-negative number of blank lines between top-level groups.
- `pnfmt_csproj_sort_item_types` replaces the built-in sortable type list. Separate names with commas or semicolons; `*` allows any homogeneous item type. Omit it to retain defaults; when extending the list, retain the desired built-in types explicitly.
- `indent_style`, `indent_size`, `tab_width`, and `end_of_line` control XML layout. `pnfmt_format = false` still permits sorting and linting.

Imports, targets, tasks, and groups nested in targets or `Choose` retain order. Forward references, conditions, property functions, nested expansions, item/metadata references, and ambiguous `Include` expressions can block sorting. Preserve sorting disabled when custom tasks depend on item order, even without visible dependencies. `--lint` reports project-structure diagnostics without writing; `CSPROJ005` for implicit default items applies only to .NET SDK projects.

Read [project documentation](../../../../docs/formatters/csproj.md) for sorting boundaries and non-SDK examples.

## Resources (`resx`)

| Setting | Behavior |
| --- | --- |
| `pnfmt_sort_entries` | Sort data and metadata entries. Leading entry comments travel with entries. |
| `pnfmt_resx_sort_comparer` | `InvariantCulture`, `InvariantCultureIgnoreCase`, `OrdinalIgnoreCase`, or `Ordinal`. |
| `pnfmt_resx_remove_xsd_schema` | Remove embedded schema when `true`. |
| `pnfmt_resx_remove_documentation_comment` | Remove standard generated documentation when `true`. |
| `pnfmt_resx_insert_xsd_schema` | Insert missing schema when `true`. |
| `pnfmt_resx_insert_documentation_comment` | Insert missing documentation when `true`. |

All transformations default off in explicit configurations and work independently of layout. Removal wins over insertion for the same content. Standard XML layout settings apply; defaults are two-space indentation, platform newlines, and no final newline. Resource values retain their parsed XML text and significant whitespace.

Without explicit supported charset, serialization follows the declared encoding or defaults to UTF-8 with a BOM. In the legacy activation path, files without layout/charset settings are rewritten only for resource transformations, and missing schema/documentation can be inserted when removal is off. Do not assume those legacy insertion defaults apply to explicit configurations.

Non-RESX XML or unnamed entries are skipped with `RESX001`, failing `--lint`; malformed XML is an execution error (`2`). See [resource documentation](../../../../docs/formatters/resx.md).

## INI and EditorConfig (`ini`)

Layout normalizes assignments to `key = value` and collapses repeated blank lines while preserving comments and unknown lines. `pnfmt_format = false` preserves assignment spacing but permits configured sorting, grouping, and merging.

- `pnfmt_sort_entries = true`: sort keys within blocks ordinally, ignoring case. Duplicate keys retain assignment order. Blank lines do not split a block; comments, headers, and unknown lines do.
- `pnfmt_ini_group_by_prefix = true`: group by text before the first underscore and add separators only for prefixes shared by at least two keys. Explicit configurations control sorting independently; without sorting, groups follow first occurrence. Legacy activation also sorts when grouping.
- `pnfmt_ini_merge_groups = true`: in `.editorconfig`, merge only adjacent sections whose headers match exactly, including case. In `.ini`, merge all same-named sections ignoring case. Contents retain occurrence order.
- `pnfmt_ini_sort_groups = true`: in `.ini`, sort named sections ordinally ignoring case, keeping the preamble first. `.editorconfig` section order is always retained to preserve glob precedence.

An `.editorconfig` containing `root = true` needs its own matching `[*.editorconfig]` section because it cannot inherit policy. Duplicate property order, including `unset`, is preserved during sorting/grouping. Generated configuration disables section sorting and merging for both file types. General INI consumers differ in repeated-section semantics; retain the task's existing policy. See [configuration formatter documentation](../../../../docs/formatters/ini.md).

## Response files (`rsp`)

`pnfmt_sort_entries = true` sorts nonempty physical lines ordinally ignoring case, with an ordinal tie-breaker. It does not tokenize/rearrange arguments within a line. Blank lines do not split sortable content; a line whose first non-whitespace character is `#` is a fixed barrier. `pnfmt_format = false` sorts without normalizing whitespace/final newlines.

Later compiler arguments may override earlier ones. Enable sorting only when line order is safe; comment barriers keep order-sensitive blocks separate. See [response-file documentation](../../../../docs/formatters/rsp.md).

## Solutions (`slnx`)

Layout uses two-space indentation independently of `pnfmt_sort_entries`, which orders recognized solution elements. Unknown extension elements are fixed barriers and their subtrees retain character data, whitespace-only values, and carriage-return character references. Containers with character data or inherited `xml:space="preserve"` retain their entire subtree, including order. `pnfmt_format = false` permits sorting without re-indenting. See [solution documentation](../../../../docs/formatters/slnx.md).

## XML and XAML (`xml`, `xaml`)

These format structural whitespace without sorting elements, attributes, namespaces, resource dictionaries, or setters. `pnfmt_sort_entries` has no effect. Original tags, quote styles, entity references, attribute layout, comments, processing instructions, and CDATA remain intact. `indent_style`, `indent_size`, `tab_width`, and `end_of_line` control layout; defaults are spaces, four columns, and detected newlines. `indent_size = tab` uses `tab_width`; tab indentation emits one tab per level. No general trailing-whitespace cleanup or line-length wrapping is applied.

Text/CDATA-containing elements preserve their entire subtree, including mixed content and whitespace-only leaf values. Inherited `xml:space="preserve"` protects the subtree even if a descendant requests `default`. Ordinary XML treats whitespace-only gaps around child markup as layout without consulting schemas; use existing preservation policy when those gaps are data.

XAML additionally protects text-oriented types such as `TextBlock`, `Run`, `Span`, and `FlowDocument`, and `.Inlines` property elements. Only recognized WPF/WinUI, Avalonia, and MAUI structural containers get child indentation. Unknown/custom/namespace-free elements retain their own whitespace; known descendants can still format outside protected subtrees. Bindings and markup extensions remain intact; runtime type/resource resolution is not validated.

Without explicit supported charset, UTF-8 and BOM-marked UTF-16/UTF-32 encodings and the declaration are preserved. Malformed XML, DTDs, and nesting beyond 256 levels are skipped with `XML001` or `XAML001`; external entities/schemas are never loaded. These diagnostics fail `--lint` but do not alone fail `--check`. Read [XML/XAML documentation](../../../../docs/formatters/xml-xaml.md) for preservation boundaries and layout limits.
