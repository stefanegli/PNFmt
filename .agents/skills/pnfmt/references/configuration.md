# Activation and compatibility

Use this reference when a file is skipped, a legacy warning appears, or a configuration migration is requested. Shared behavior defaults, generated configuration, encoding, and final-newline rules are in [SKILL.md](../SKILL.md).

## Explicit activation and inheritance

Configure both controls in the matching EditorConfig section:

```ini
[*.csproj]
pnfmt_enabled = true
pnfmt_formatter = csproj
pnfmt_sort_entries = true

[*.config]
pnfmt_enabled = true
pnfmt_formatter = xml

[Generated/**]
pnfmt_enabled = false
```

Enablement, selection, and behavior settings inherit independently. Disabling processing preserves the inherited formatter and options, so a descendant can re-enable them. `unset` removes an inherited setting. Boolean switches accept `true` and `false` case-insensitively; an explicit `false` overrides an inherited `true`. Parameters such as sort order and indentation do not activate their associated behavior.

## Missing activation controls

A formatter selection alone still enables processing. `pnfmt_enabled = true` without a selection uses the file extension. Either case emits `PNFMT004`; the warning also appears for activation under the legacy rules below, even without `--verbose`, and does not itself change the exit code. This compatibility path is deprecated; configure both controls when configuration changes are in scope.

When both controls are missing or `unset`, the extension selects the formatter and these rules determine activation:

| Formatter | Legacy activation |
| --- | --- |
| C# | `pnfmt_csharp_format = true`; other cleanup switches alone do not activate it. |
| Project | A project sorting/group setting or a standard layout setting as detailed below. `--lint` also activates it. |
| INI/EditorConfig | At least one of `pnfmt_sort_entries`, `pnfmt_ini_group_by_prefix`, `pnfmt_ini_merge_groups`, or `pnfmt_ini_sort_groups` is `true`. |
| Resource | Presence of `pnfmt_sort_entries`, `pnfmt_resx_remove_xsd_schema`, or `pnfmt_resx_remove_documentation_comment`, even when `false`. Comparer, layout, charset, and insertion settings alone do not activate it. |
| Response, solution | `pnfmt_sort_entries = true`. |
| XML | `pnfmt_xml_format = true`. |
| XAML | `pnfmt_xaml_format = true`. |

For projects, presence of `pnfmt_sort_entries`, `pnfmt_csproj_sort_item_types`, `indent_style`, or `end_of_line` activates legacy processing. So do a positive `indent_size` or `tab_width`, a valid boolean `insert_final_newline`, or a non-negative integer `pnfmt_csproj_empty_lines_between_groups`. Supported legacy aliases also count. `pnfmt_sort_entries = false` disables sorting while allowing layout. `charset` alone never activates processing.

Legacy INI prefix grouping also sorts keys. Legacy RESX processing can insert missing schema/documentation when removal is disabled, and without explicit layout or charset settings it rewrites only for resource transformations. Explicit activation uses independent switches for these behaviors; see [other formatters](other-formatters.md) for preservation rules.

## Legacy names and migration

The current name takes precedence over its alias, including when explicitly `false`. `PNFMT001` reports legacy names whether used or ignored. After inheritance removes a current name with `unset`, a remaining legacy alias can apply.

| Legacy name | Current name |
| --- | --- |
| `csproj_formatter_sort_entries` | `pnfmt_sort_entries` |
| `csproj_formatter_empty_lines_between_groups` | `pnfmt_csproj_empty_lines_between_groups` |
| `csproj_formatter_sort_item_types` | `pnfmt_csproj_sort_item_types` |
| `resx_formatter_sort_entries` | `pnfmt_sort_entries` |
| `resx_formatter_remove_xsd_schema` | `pnfmt_resx_remove_xsd_schema` |
| `resx_formatter_remove_documentation_comment` | `pnfmt_resx_remove_documentation_comment` |
| `resx_formatter_sort_comparer` | `pnfmt_resx_sort_comparer` |

`--write-default-config` asks separately about migrating and removing legacy settings. Supply both choices for non-interactive use, for example to migrate while retaining aliases:

```powershell
pnfmt --write-default-config --migrate-legacy-config=true --remove-legacy-config=false .
```

Migration adds missing current names with legacy values in the same section; existing current names are preserved. Conflicting legacy values mapping to the same missing current name in one section are an error. Removal is independent and should be enabled only when deleting old names is in scope. The command also fills missing generated defaults; it is not a migration-only operation.
