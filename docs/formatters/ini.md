# Configuration formatter (`.editorconfig`, `.ini`)

The configuration formatter normalizes assignments and can sort properties, group related keys, merge duplicate sections, and sort sections. Each behavior is independently configurable through EditorConfig.

## Configuration

An `.editorconfig` containing `root = true` must include its own matching `[*.editorconfig]` section because it cannot inherit settings from a parent file.

```ini
[*.editorconfig]
pnfmt_ini_merge_groups = true
pnfmt_sort_entries = true

[*.ini]
pnfmt_ini_group_by_prefix = true
pnfmt_ini_merge_groups = true
pnfmt_ini_sort_groups = true
pnfmt_sort_entries = true
```

| Setting | Description |
| --- | --- |
| `pnfmt_sort_entries` | Sort keys within property blocks. |
| `pnfmt_ini_group_by_prefix` | Sort keys and group them by the text before their first underscore. Prefixes shared by at least two keys receive blank-line separators; singleton prefixes remain ungrouped. |
| `pnfmt_ini_merge_groups` | Merge sections with the same header, ignoring case. Their contents retain occurrence order and are formatted as one section. |
| `pnfmt_ini_sort_groups` | Sort named sections by header using ordinal, case-insensitive comparison. The preamble remains at the top. |

At least one of these settings must be `true` for the formatter to run. Missing, invalid, and `false` values do not activate it.

## Behavior

Assignments are normalized to `key = value`, repeated blank lines are collapsed, and comments and unknown lines are preserved. Empty lines do not divide a property block; comments, section headers, and unknown lines do.

Prefix grouping also enables property sorting. A group receives separators only when at least two keys share its prefix, so isolated keys do not create one-line groups.

Section merging and sorting can change how duplicate INI sections are interpreted. In `.editorconfig`, section order also controls precedence between matching patterns. Enable these behaviors only when the resulting order is safe.

[Back to the overview](../../README.md)
