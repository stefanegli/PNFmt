# C# formatter (`.cs`)

The C# formatter uses Roslyn to format individual files. It does not load MSBuild projects, solutions, assembly references, or analyzers from the target repository. Unresolved types do not prevent formatting.

## Configuration

```ini
[*.cs]
pnfmt_csharp_format = true
pnfmt_csharp_sort_modifiers = true
pnfmt_csharp_sort_members = true
pnfmt_csharp_collapse_blank_lines = true
pnfmt_sort_entries = true

indent_style = space
indent_size = 4
tab_width = 4
end_of_line = crlf
insert_final_newline = true
trim_trailing_whitespace = true

csharp_new_line_before_open_brace = all
csharp_space_after_keywords_in_control_flow_statements = true
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = false
```

Only `pnfmt_csharp_format = true` activates this formatter. Missing, invalid, and `false` values leave the file unchanged. Import sorting, member sorting, modifier ordering, and blank-line cleanup each require their own setting to be `true`; none activates C# formatting on its own. `--write-default-config` adds these enabled settings and the member-order defaults below to `[*.cs]`, preserving existing values.

Run only this formatter with:

```powershell
pnfmt --formatter csharp --recursive .
pnfmt --formatter csharp --check --recursive .
```

The usual Git changed-file selection, `--all`, `--file-pattern`, `--dry-run`, and parallel processing options apply.

## Whitespace formatting

PNFmt passes the resolved EditorConfig settings to Roslyn 5.0's C# whitespace formatter. This supports the standard [C# indentation, spacing, newline, and wrapping options](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/csharp-formatting-options). Code-style preferences that require rewriting declarations or expressions are not applied, apart from the explicitly enabled sorting described below. Formatting does not impose a maximum line length or reflow comments.

Without explicit settings, formatting uses spaces, four-column indentation and tab width, the file's detected newline convention, and Roslyn's remaining formatting defaults. `insert_final_newline = true` adds a missing final newline to nonempty files; missing or false preserves the existing final-newline state. `trim_trailing_whitespace = true` removes trailing spaces and tabs from ordinary code whitespace. Explicit `end_of_line` values are `lf`, `crlf`, and `cr`.

Multiline string contents, comments, directives, and disabled preprocessor text are protected from the additional whitespace cleanup. Line-ending normalization therefore does not necessarily make every newline in a file identical. Roslyn may adjust comment indentation, but literal token text, directives, and inactive code must remain unchanged; PNFmt skips a file if this protection check fails.

## Import sorting

Sorting applies separately to compilation-unit imports and imports inside block or file-scoped namespaces. Imports remain in their original scope. Global imports remain separate from ordinary imports; `extern alias` directives stay in place.

Within each sortable run:

- Ordinary imports precede `using static` imports, followed by aliases.
- Names use ordinal, case-sensitive comparison; aliases sort by alias name.
- `System` and its child namespaces come first within the ordinary/static categories by default. Set `dotnet_sort_system_directives_first = false` for purely alphabetical order within each category.
- `dotnet_separate_import_directive_groups = true` inserts a blank line between different root namespaces or import categories. Blank lines alone are not sorting barriers.
- Duplicate imports are retained.

Leading headers and standalone comment sections remain anchored at the start of their runs. Imports cannot cross those comments or preprocessor directives such as `#if`, `#region`, `#nullable`, and `#pragma`. A trailing `//` comment travels with its import. Imports containing internal comments or other non-whitespace trivia, or trailing block comments, stay in place and divide runs.

Import sorting does not reorder members, enum values, or statements. Unused imports are not removed, names are not simplified, and types are not replaced with `var`.

## Member sorting

PNFmt uses standard .NET option names wherever the behavior has an equivalent. The current [.NET code-style rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/language-rules) do not provide member-kind, member-accessibility, or alphabetical declaration ordering options, so these preferences use `pnfmt_csharp_*` settings. The standard [`csharp_preferred_modifier_order`](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/ide0036) option orders keywords within a declaration, such as `public static`; it does not determine whether public methods precede private methods. PNFmt already uses that option for modifier ordering and the standard `dotnet_*` import-ordering options.

`pnfmt_csharp_sort_members = true` sorts consecutive movable declarations inside classes, structs, interfaces, and records, including nested types. The default keys are **member kind, accessibility, then name**. Names use ordinal, case-sensitive comparison. Equal keys retain their original order, so overloads with the same accessibility stay in their existing order. Static and instance members are not separated.

```ini
[*.cs]
pnfmt_csharp_format = true
pnfmt_csharp_sort_members = true
pnfmt_csharp_member_order = constant,constructor,destructor,property,indexer,event,method,operator,conversion_operator,type
pnfmt_csharp_member_accessibility_order = public,internal,protected_internal,protected,private_protected,private
pnfmt_csharp_sort_members_by_name = true
```

The default order puts constants first, followed by constructors, finalizers, properties, indexers, events with explicit accessors, methods, operators, conversion operators, and nested types (including delegates and enums). For each kind, public members come first and private members last. Omitted access modifiers use the C# default: private inside classes, structs, and records; public inside interfaces. Finalizers use protected accessibility. A property's accessor visibility does not change the property's rank.

Rearrange either comma-separated list to select a different order. Both lists accept case-insensitive names and surrounding spaces. Omitted kinds or accessibilities stay in place and divide sortable runs. An empty list, duplicate name, or unsupported name disables member sorting for the file; missing or `unset` values use the defaults. Set `pnfmt_csharp_member_accessibility_order = none` to ignore accessibility, or `pnfmt_csharp_sort_members_by_name = false` to retain source order within each kind/accessibility group. These options do not activate member sorting on their own.

For example, to sort only methods, put private methods first, and retain source order within each accessibility:

```ini
pnfmt_csharp_member_order = method
pnfmt_csharp_member_accessibility_order = private,private_protected,protected,protected_internal,internal,public
pnfmt_csharp_sort_members_by_name = false
```

The following declarations and boundaries stay in place regardless of the configured order:

- Non-constant fields, fixed buffers, and field-like events, even without initializers. Variables within a declaration are never split or reordered.
- Auto-properties, properties with initializers, and C# 14 properties using `field`. Their backing fields can affect initialization and layout. Computed properties and abstract/interface properties can move.
- All properties declared in records, because synthesized `ToString()` output also depends on property order.
- Members containing preprocessor directives or inactive text, and members intersecting `// pnfmt: off` regions. Members cannot cross these boundaries.
- Methods marked with `ModuleInitializer` or `ModuleInitializerAttribute`, and nested types containing them, so their execution order is retained. Attribute names reached through a differently named alias cannot be recognized without resolving symbols.
- Members with leading ordinary comments, which may label a section. XML documentation, attributes, body comments, and trailing comments travel with movable declarations. Blank-line spacing stays with the declaration slots.
- Unsupported member syntax, including extension blocks. Attributed interfaces retain their member order because they may define a COM vtable.

Field and property initializers run in [declaration order](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/classes#1556-variable-initializers). Keeping storage declarations in their original slots preserves that order and the relative order of backing fields; there is no configuration to bypass these protections. As a result, a field or auto-property between two methods prevents those methods from swapping across it.

Top-level types, enum values, statements, local functions, and accessors retain their order. Explicit interface implementations sort by qualified interface/member name; operators sort by their token and conversion operators by `implicit`/`explicit`. This is a file-local syntax transformation: it cannot account for source generators, reflection consumers, or attributes defined elsewhere that depend on source or metadata order. Disable member sorting for such files or use exclusion regions for affected code.

## Modifier ordering

`pnfmt_csharp_sort_modifiers = true` orders modifiers on member declarations and local functions without adding or removing any modifier. For example, `static public` becomes `public static`. The standard `csharp_preferred_modifier_order` setting selects the order and accepts an optional severity suffix such as `:suggestion`; the PNFmt switch controls activation regardless of that suffix.

The default order is:

```ini
csharp_preferred_modifier_order = public,private,protected,internal,file,static,extern,new,virtual,abstract,sealed,override,readonly,unsafe,required,volatile,async
```

`partial` always stays last so it remains next to the declaration keyword. Comments or directives between modifiers prevent their reordering. Leading headers and attributes stay in place. A list with any unknown or unranked modifier, including `ref`, is left alone. A configured order containing duplicate or unsupported names disables this cleanup. Parameters and accessors are not reordered. Excluded regions are respected.

## Blank-line cleanup

`pnfmt_csharp_collapse_blank_lines = true` reduces two or more empty lines between adjacent member or type declarations to one empty line. It does not insert a blank line where none existed. Only gaps consisting entirely of whitespace are changed; gaps containing comments or directives are preserved. Blank lines inside method bodies, top-level statements, strings, comments, inactive code, and excluded regions are not collapsed.

## Exclusion regions

Standalone, case-sensitive `// pnfmt: off` and `// pnfmt: on` comments protect the complete marked lines and everything between them. Their contents retain their exact text, including whitespace and line endings, even when other settings request cleanup. Imports cannot move across or within a protected region.

```csharp
// pnfmt: off
int[] columns = [  1,  20, 300 ];
// pnfmt: on
```

Markers can be indented and can have trailing whitespace. Nested pairs are supported. An unmatched `off` protects through EOF, including a missing final newline; an unmatched `on` has no effect. Marker-like text in strings, block comments, trailing comments, or inactive preprocessor branches is not interpreted as a marker. The whole file must still parse successfully before formatting proceeds.

## Parse policy and diagnostics

Files are parsed as regular C# 14 source, with no project-defined preprocessor symbols. File-local `#define` directives still apply. Inactive `#if` branches are preserved verbatim, including branches that would be active in another build configuration. PNFmt does not attempt to format every conditional-compilation configuration.

| Diagnostic | Behavior |
| --- | --- |
| `PNFMT002` | Syntax errors: skip the whole file and report the first parser error and line number. |
| `PNFMT003` | Formatting would change protected text or produce invalid syntax: skip the whole file. |

These diagnostics are printed in all modes. `--lint` returns exit code 1 when they occur. A skipped file alone does not fail ordinary formatting or `--check`; the latter fails when formatting changes are needed. Preview modes never write the source file.

## Encoding and generated files

C# formatting honors the shared [EditorConfig charset settings](../../README.md#file-encoding). Without a supported charset, it preserves UTF-8 with or without a BOM, and BOM-marked UTF-16/UTF-32 in either byte order. BOM-less input must be valid UTF-8 unless Latin-1 is explicitly configured. Undecodable input or unrepresentable output is reported as an error and is never rewritten with replacement characters.

Files ending in `.g.cs`, `.g.i.cs`, `.generated.cs`, or `.designer.cs`, or with a leading comment containing `<auto-generated` or `<autogenerated`, are skipped automatically. Name and header matching is case-insensitive. Explicit `generated_code = true` forces skipping; `generated_code = false` overrides automatic detection. Generated files are skipped before syntax validation.

You can also disable formatting with a more specific EditorConfig section:

```ini
[*.{g,designer}.cs]
pnfmt_csharp_format = false
```

[Back to the overview](../../README.md)
