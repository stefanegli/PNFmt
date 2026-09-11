# Project formatter (`.csproj`)

The project formatter normalizes MSBuild project XML, sorts eligible properties and items, and keeps evaluation-sensitive content in its original order. It is also the only formatter that supports `--lint` diagnostics.

## Configuration

```ini
[*.csproj]
end_of_line = crlf
indent_style = space
pnfmt_csproj_empty_lines_between_groups = 1
pnfmt_sort_entries = true
tab_width = 4
```

| Setting | Description |
| --- | --- |
| `pnfmt_sort_entries` | Sort eligible properties and items when set to `true`. |
| `pnfmt_csproj_empty_lines_between_groups` | Set the non-negative number of empty lines between top-level groups. |
| `pnfmt_csproj_sort_item_types` | Replace the built-in list of sortable item types. Separate names with commas or semicolons, or use `*` for any homogeneous item type. |
| `indent_style`, `indent_size`, `tab_width`, `end_of_line` | Control standard XML layout through EditorConfig. |
| `insert_final_newline` | End the file with one configured newline when `true` (the default), or omit the final newline when `false`. |

Omit `pnfmt_csproj_sort_item_types` to use the built-in item-type list.

## Behavior

Property and item sorting is deliberately limited to content that can be reordered without changing normal MSBuild evaluation. Forward property references, item operations, imports, conditions, and other evaluation-sensitive constructs retain their meaningful order.

Property groups containing property functions, member access, or nested property expansions retain their original order because those dependencies cannot be resolved reliably without evaluating the project.

Run `pnfmt --lint <paths>` to report project-structure diagnostics and formatting changes without writing files. The command returns exit code `1` when it finds either.

[Back to the overview](../../README.md)
