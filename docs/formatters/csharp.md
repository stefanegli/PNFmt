# C# formatter (`.cs`)

The C# formatter uses Roslyn to format individual files. It does not load MSBuild projects, solutions, assembly references, or analyzers from the target repository. Unresolved types do not prevent formatting.

## Configuration

```ini
[*.cs]
pnfmt_csharp_format = true
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

Only `pnfmt_csharp_format = true` activates this formatter. Missing, invalid, and `false` values leave the file unchanged. `pnfmt_sort_entries = true` additionally enables import sorting; it does not activate C# formatting on its own. `--write-default-config` adds both activation and sorting settings to `[*.cs]`, preserving existing values.

Run only this formatter with:

```powershell
pnfmt --formatter csharp --recursive .
pnfmt --formatter csharp --check --recursive .
```

The usual Git changed-file selection, `--all`, `--file-pattern`, `--dry-run`, and parallel processing options apply.

## Whitespace formatting

PNFmt passes the resolved EditorConfig settings to Roslyn 5.0's C# whitespace formatter. This supports the standard [C# indentation, spacing, newline, and wrapping options](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/csharp-formatting-options). Code-style preferences that require rewriting declarations or expressions are not applied. Formatting does not impose a maximum line length or reflow comments.

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

Members, modifiers, enum values, and statements are not reordered. Unused imports are not removed, names are not simplified, and types are not replaced with `var`.

## Parse policy and diagnostics

Files are parsed as regular C# 14 source, with no project-defined preprocessor symbols. File-local `#define` directives still apply. Inactive `#if` branches are preserved verbatim, including branches that would be active in another build configuration. PNFmt does not attempt to format every conditional-compilation configuration.

| Diagnostic | Behavior |
| --- | --- |
| `PNFMT002` | Syntax errors: skip the whole file and report the first parser error and line number. |
| `PNFMT003` | Formatting would change protected text or produce invalid syntax: skip the whole file. |

These diagnostics are printed in all modes. `--lint` returns exit code 1 when they occur. A skipped file alone does not fail ordinary formatting or `--check`; the latter fails when formatting changes are needed. Preview modes never write the source file.

## Encoding and generated files

C# formatting preserves UTF-8 with or without a BOM, and BOM-marked UTF-16/UTF-32 in either byte order. BOM-less input must be valid UTF-8; undecodable input is reported as an error and is never rewritten with replacement characters. Legacy code pages are not guessed. These encoding rules apply to the C# formatter.

Files ending in `.g.cs`, `.g.i.cs`, `.generated.cs`, or `.designer.cs`, or with a leading comment containing `<auto-generated` or `<autogenerated`, are skipped automatically. Name and header matching is case-insensitive. Explicit `generated_code = true` forces skipping; `generated_code = false` overrides automatic detection. Generated files are skipped before syntax validation.

You can also disable formatting with a more specific EditorConfig section:

```ini
[*.{g,designer}.cs]
pnfmt_csharp_format = false
```

[Back to the overview](../../README.md)
