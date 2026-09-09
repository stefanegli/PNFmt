# Configuration formatter (`.editorconfig`, `.ini`)

The configuration formatter normalizes assignments and can sort properties, group related keys, merge duplicate sections, and sort sections. Each behavior is independently configurable through EditorConfig.

## Configuration

An `.editorconfig` containing `root = true` must include its own matching `[*.editorconfig]` section because it cannot inherit settings from a parent file.

```ini
[*.editorconfig]
pnfmt_ini_merge_groups = false
pnfmt_ini_sort_groups = false
pnfmt_sort_entries = true

[*.ini]
pnfmt_ini_group_by_prefix = true
pnfmt_ini_merge_groups = false
pnfmt_ini_sort_groups = false
pnfmt_sort_entries = true
```

| Setting | Description |
| --- | --- |
| `pnfmt_sort_entries` | Sort keys within property blocks using stable, ordinal, case-insensitive comparison. Duplicate keys retain assignment order. |
| `pnfmt_ini_group_by_prefix` | Sort keys and group them by the text before their first underscore. Prefixes shared by at least two keys receive blank-line separators; singleton prefixes remain ungrouped. |
| `pnfmt_ini_merge_groups` | In `.editorconfig`, merge only adjacent sections with exactly the same header, including case. In `.ini`, merge all sections with the same header, ignoring case. Contents retain occurrence order. |
| `pnfmt_ini_sort_groups` | In `.ini`, sort named sections by header using ordinal, case-insensitive comparison, keeping the preamble at the top. In `.editorconfig`, section sorting is skipped to preserve precedence. |

At least one of these settings must be `true` for the formatter to run. Missing, invalid, and `false` values do not activate it.

## Behavior

Assignments are normalized to `key = value`, repeated blank lines are collapsed, and comments and unknown lines are preserved. Empty lines do not divide a property block; comments, section headers, and unknown lines do.

Prefix grouping also enables property sorting. A group receives separators only when at least two keys share its prefix, so isolated keys do not create one-line groups.

EditorConfig sections retain their relative order even when section sorting is requested. Nonadjacent sections are never merged: moving assignments across an intervening section can change which value wins for overlapping globs. Adjacent sections with identical headers can be merged safely. Differently cased glob headers remain distinct. Key sorting and prefix grouping preserve the order of duplicate property names, comparing names case-insensitively, including assignments to `unset`.

`--write-default-config` explicitly disables section sorting and merging for both file types and preserves any existing setting values. General INI consumers differ in how they interpret repeated sections and keys, so enable section transformations only after checking the consuming application's rules. Explicitly enabled INI merging and sorting retain their existing behavior.

[Back to the overview](../../README.md)
