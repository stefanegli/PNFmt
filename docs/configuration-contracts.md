# Formatter activation and final newlines

Configure enablement, formatter selection, and behaviors separately in the applicable `.editorconfig` section:

```ini
[*.csproj]
pnfmt_enabled = true
pnfmt_formatter = csproj
pnfmt_format = true
pnfmt_sort_entries = true

[*.config]
pnfmt_enabled = true
pnfmt_formatter = xml

[Generated/**]
pnfmt_enabled = false
```

`pnfmt_enabled` controls whether the file is processed. `false` skips all processing, including linting and encoding changes, without clearing the formatter choice or options. `pnfmt_formatter` selects `csharp`, `csproj`, `ini`, `resx`, `rsp`, `slnx`, `xml`, or `xaml`, independently of the extension. Names are case-insensitive. `None` remains accepted as a compatibility way to disable processing. Unknown or empty formatter names are configuration errors when processing is enabled.

Normal EditorConfig inheritance applies to each setting independently. A child can set `pnfmt_enabled = false` and a descendant can re-enable processing while retaining the inherited formatter and options. `unset` removes an inherited setting. The CLI `--formatter` option filters the resolved formatter names; it does not override enablement or select a formatter for a file.

Behavior switches are independent of enablement:

| Switch | Applies to | Default when omitted |
| --- | --- | --- |
| `pnfmt_format` | Layout formatting in every formatter | `true` |
| `pnfmt_sort_entries` | C# imports, project entries, INI properties, resource entries, response lines, solution elements | `false` |
| `pnfmt_csharp_sort_modifiers`, `pnfmt_csharp_sort_members`, `pnfmt_csharp_collapse_blank_lines`, `pnfmt_csharp_remove_regions` | Individual C# transformations | `false` |
| `pnfmt_ini_group_by_prefix`, `pnfmt_ini_sort_groups`, `pnfmt_ini_merge_groups` | Individual INI transformations | `false` |
| `pnfmt_resx_remove_xsd_schema`, `pnfmt_resx_remove_documentation_comment` | Remove resource schema or documentation | `false` |
| `pnfmt_resx_insert_xsd_schema`, `pnfmt_resx_insert_documentation_comment` | Insert missing resource schema or documentation | `false` |

Every switch accepts `true` or `false`, case-insensitively. `false` overrides an inherited `true`. Parameter settings such as indentation size, member order, and sort comparer configure their associated behavior; they do not turn it on. Member sorting retains its established secondary defaults, including sorting names within each configured member group.

Set `pnfmt_format = false` to run sorting or cleanup without the normal layout pass. If all behaviors are off and no encoding change is configured, the file stays unchanged. Sorting and cleanup can move associated whitespace, and XML content edits still use XML serialization. Explicit `charset` remains an independent encoding request. The existing `pnfmt_csharp_format`, `pnfmt_xml_format`, and `pnfmt_xaml_format` switches remain layout fallbacks; `pnfmt_format` takes precedence.

For this version, a missing enablement or selection retains compatibility behavior. A selection alone still enables its formatter; `pnfmt_enabled = true` without a selection still uses the extension. If both are missing, the legacy activation rules below apply. Each such activation prints warning `PNFMT004`, even without `--verbose`, without changing exit codes. A future version will require both explicit enablement and selection. Missing values include `unset`.

`--write-default-config` writes enablement, selection, and explicit behavior choices. It preserves existing values, including `pnfmt_enabled = false` and formatter `None`. Its generated configuration explicitly turns on several optional behaviors; these are distinct from the defaults used when those switches are absent.

| Formatter | Legacy activation when enablement and selection are both missing |
| --- | --- |
| C# | `pnfmt_csharp_format = true`. Other cleanup switches alone do not activate it. |
| Project | A project-specific setting or standard layout setting. `pnfmt_sort_entries = false` disables sorting while allowing XML layout formatting. See the [project settings](formatters/csproj.md). |
| Configuration | At least one of `pnfmt_sort_entries`, `pnfmt_ini_group_by_prefix`, `pnfmt_ini_merge_groups`, or `pnfmt_ini_sort_groups` is `true`. |
| Resource | Presence of `pnfmt_sort_entries`, `pnfmt_resx_remove_xsd_schema`, or `pnfmt_resx_remove_documentation_comment`, including `false`. The comparer, layout, and charset settings alone do not activate it. |
| Response, solution | `pnfmt_sort_entries = true`. |
| XML | `pnfmt_xml_format = true`. |
| XAML | `pnfmt_xaml_format = true`. |

Legacy project and resource setting names remain accepted as fallbacks. `charset` alone never activates a formatter. A setting removed by EditorConfig's `unset` is treated as absent. Project `--lint` also retains its legacy activation when both controls are missing, with the same warning. Legacy INI prefix grouping also sorts properties; explicit configurations can group without sorting. Legacy RESX behavior may insert missing schema/documentation when removal is disabled; explicit configurations require the separate insertion switches. Removal takes precedence if both insertion and removal are enabled for the same content.

When layout formatting is on, `insert_final_newline` has these effects for enabled, nonempty files that are rewritten:

| Formatter | `true` | `false` | Missing |
| --- | --- | --- | --- |
| C#, XML, XAML | Add a missing final newline. | Preserve whether one existed. | Preserve whether one existed. |
| Project | End with one newline. | Remove the final newline. | End with one newline. |
| Resource | End with one newline. | Remove the final newline. | Omit the final newline. |
| Configuration, response, solution | End with one newline. | End with one newline. | End with one newline. |

C# exclusion regions and protected XML content can prevent a requested whitespace change; their preservation rules still apply. Legacy resource formatting without explicit layout or charset settings rewrites only when resource sorting or schema/documentation changes are needed.

Configuration, response, and solution formatters use the input's detected line ending and do not apply `end_of_line` or indentation settings. The solution formatter uses two spaces for known XML containers. C#, project, resource, XML, and XAML formatters support explicit layout settings as described on their individual pages.

[Back to the overview](../README.md)
