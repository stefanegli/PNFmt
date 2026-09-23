# C# formatting

Use this reference when configuring or troubleshooting the `csharp` formatter. Shared activation, encoding, final-newline rules, and CLI behavior are in [SKILL.md](../SKILL.md).

## Layout and scope

PNFmt formats one file at a time using Roslyn 5.9 and regular C# 14 parsing. It does not load projects, metadata references, target analyzers, or project-defined preprocessor symbols. Unresolved types are acceptable; file-local `#define` directives apply. Do not assume arbitrary analyzer code fixes run: brace insertion, namespace conversion, unused-import removal, `var` conversion, and name simplification are not implemented.

Standard Roslyn indentation, spacing, newline, and wrapping preferences apply, including `csharp_new_line_before_open_brace` and `csharp_space_after_keywords_in_control_flow_statements`. Without explicit layout settings, indentation and tab width are four columns, using spaces and the detected newline. `end_of_line` accepts `lf`, `crlf`, or `cr`; `trim_trailing_whitespace = true` trims ordinary code whitespace. Comment contents, literal tokens, retained directives, and inactive text are protected from additional cleanup, although Roslyn can adjust comment indentation.

`pnfmt_format = false` disables layout, width/list wrapping, Microsoft blank-line preferences, and file headers. Independently enabled import, modifier, and member sorting, declaration blank-line cleanup, and region removal still run. None of these options supplies missing explicit file activation.

## Width and list wrapping

`max_line_length` enables wrapping when it is a positive integer. Missing, `unset`, invalid, zero, or negative widths disable width-based wrapping. A value such as `120` is a policy choice, not a built-in maximum. Width includes indentation and tab stops; trailing comments do not force wrapping. The width is a target: unbreakable identifiers, literals, and unsupported constructs may exceed it.

Supported break points are argument/parameter lists, chained calls (before `.` or the complete `?.`), and binary expressions. `dotnet_style_operator_placement_when_wrapping` accepts `beginning_of_line` (default) or `end_of_line`. Continuations use one extra indentation level and the configured/detected newline. Existing breaks remain; comments are not reflowed. Closing list delimiters stay with the last item unless already on a separate line.

Configure declarations with `csharp_wrap_parameters_style` and calls/element access with `csharp_wrap_arguments_style`. These are JetBrains option names, not Microsoft options. Both accept case-insensitive values:

| Value | Effect |
| --- | --- |
| `wrap_if_long` | Fit as many whole items as possible per line when exceeding a valid width. |
| `chop_if_long` | One item per line when too long or already multiline; existing multiline lists do not require a width. |
| `chop_always` | One item per line regardless of width. |

Chopping breaks after the opening delimiter even for one item; empty lists remain empty. Parameters include constructors, records, delegates, lambdas, and indexers; arguments include object creation and element access. `resharper_csharp_wrap_parameters_style` and `resharper_csharp_wrap_arguments_style` are aliases. The spelling without `resharper_` takes precedence when present, even if invalid; `unset` removes that spelling through inheritance. Missing or invalid styles retain width-triggered chopping and preserve shorter multiline layouts.

Wrapping only changes ordinary whitespace between existing tokens. Protected boundaries can prevent a requested break. A list or expression intersecting an exclusion is left alone by this pass.

## Sorting and region removal

All activation switches below default off. Parameters configure a behavior without activating it.

| Switch | Parameters and behavior |
| --- | --- |
| `pnfmt_sort_entries` | Sort imports within each scope: ordinary, static, then aliases. `dotnet_sort_system_directives_first` defaults to `true`; `dotnet_separate_import_directive_groups = true` separates root namespaces/categories with blank lines. Global imports stay separate; duplicates remain. |
| `pnfmt_csharp_sort_modifiers` | Order existing declaration/local-function modifiers using `csharp_preferred_modifier_order`, which accepts a severity suffix. `partial` stays last. Comments, directives, unsupported modifiers, or invalid orders prevent reordering. |
| `pnfmt_csharp_sort_members` | Sort consecutive movable type members by kind, accessibility, then ordinal name. Configure `pnfmt_csharp_member_order`, `pnfmt_csharp_member_accessibility_order`, and `pnfmt_csharp_sort_members_by_name`. |
| `pnfmt_csharp_remove_regions` | Remove complete active `#region`/`#endregion` pairs before sorting. If either directive is inactive or excluded, retain the pair. Other directives and enclosed code remain. |

Member-order defaults:

```ini
pnfmt_csharp_member_order = constant,constructor,destructor,property,indexer,event,method,operator,conversion_operator,type
pnfmt_csharp_member_accessibility_order = public,internal,protected_internal,protected,private_protected,private
pnfmt_csharp_sort_members_by_name = true
```

Lists accept case-insensitive comma-separated names. Omitted kinds/accessibilities stay in place and split runs; empty, duplicate, or unsupported entries disable member sorting. Missing or `unset` uses defaults. Accessibility `none` ignores that key; name sorting `false` retains source order within a group. Equal keys preserve overload order.

Omitted access modifiers use the C# default: private in classes, structs, and records; public in interfaces. Finalizers rank as protected. Accessor visibility does not change a property's rank. Static and instance members are not separated. Explicit interface implementations sort by qualified name, operators by token, and conversions by `implicit`/`explicit`.

For example, sort only methods, put private methods first, and retain source order within each accessibility:

```ini
[*.cs]
pnfmt_enabled = true
pnfmt_formatter = csharp
pnfmt_csharp_sort_members = true
pnfmt_csharp_member_order = method
pnfmt_csharp_member_accessibility_order = private,private_protected,protected,protected_internal,internal,public
pnfmt_csharp_sort_members_by_name = false
```

Import sorting retains scope and comment/directive barriers. Member sorting keeps nonconstant storage declarations, auto/initialized/`field` properties, record properties, ordinary comment headers, directives, exclusions, recognized module initializers, and unsupported syntax in place. Attributed interfaces retain member order. XML documentation and attributes travel with movable declarations. Top-level types, statements, local functions, enum values, and accessors are not sorted. This syntax-only transformation cannot resolve aliases of special attributes or account for reflection/source-generator dependencies; retain existing policy for such code.

## Blank lines

`pnfmt_csharp_collapse_blank_lines = true` independently collapses repeated blank lines only in whitespace-only gaps between adjacent member/type declarations. It does not insert missing blank lines or collapse method-body gaps.

For layout throughout ordinary code, use these Microsoft experimental settings:

| Setting | Effect of `false` |
| --- | --- |
| `dotnet_style_allow_multiple_blank_lines_experimental` | Collapse multiple blank lines to one, including within method bodies and around comments/directives. |
| `dotnet_style_allow_statement_immediately_after_block_experimental` | Add a blank line between a completed block/switch and a following statement on another line. Keep `else`, `catch`, `finally`, and `do`/`while` continuations together. |
| `csharp_style_allow_blank_lines_between_consecutive_braces_experimental` | Remove blank lines between adjacent closing braces when the gap contains only whitespace. |

Missing, `true`, `unset`, or invalid values leave the existing spacing alone. Values are case-insensitive and allow a severity suffix such as `false:warning`; formatting uses the preference regardless of severity. A comment/directive before a subsequent statement blocks insertion; a trailing closing-brace comment stays attached. These preferences preserve literal/comment contents, retained directives, inactive source, and exclusions.

## File headers

`file_header_template` adds or updates a header; missing, empty, or `unset` templates do nothing. Supply text without comment delimiters. Literal `\n` separates template lines and `{fileName}` expands to the basename including extension:

```ini
file_header_template = Copyright (c) Example.\n{fileName}\nAll rights reserved.
```

PNFmt writes `//` comments, a bare `//` for blank template lines, and one blank line before remaining content. Newlines follow layout settings. The initial ordinary line-comment group or block comment is replaceable header text; a blank line ends the line-comment group. Matching headers retain their style after comparison ignoring surrounding line whitespace and block-comment `*` decoration. XML documentation, later comment groups, directives, and formatter markers remain. Preview/check report header changes without writing.

## Exclusions, generated files, and diagnostics

Standalone, case-sensitive `// pnfmt: off` and `// pnfmt: on` protect complete marked lines and intervening text. Nesting is supported; unmatched `off` protects through EOF, while unmatched `on` has no effect. Marker-like text in strings, trailing/block comments, or inactive branches is not a marker.

Generated files are skipped before parsing: suffixes `.g.cs`, `.g.i.cs`, `.generated.cs`, `.designer.cs`, or leading comments containing `<auto-generated`/`<autogenerated`, ignoring case. `generated_code = true` forces skipping; `false` overrides detection. Without an explicit supported charset, C# preserves UTF-8 and BOM-marked UTF-16/UTF-32 encodings.

`PNFMT002` skips syntax errors; `PNFMT003` skips a result that changes protected text or introduces invalid syntax. Both appear in all modes and fail `--lint`. A skipped file alone does not fail `--check`.
