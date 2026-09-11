# Formatter activation and final newlines

The formatters retain their established activation and newline rules. In particular, a shared `[*]` section with standard indentation or newline settings can activate project formatting. Check the table when applying settings across file types.

| Formatter | What activates formatting |
| --- | --- |
| C# | `pnfmt_csharp_format = true`. Other cleanup switches alone do not activate it. |
| Project | A project-specific setting or standard layout setting. `pnfmt_sort_entries = false` disables sorting while allowing XML layout formatting. See the [project settings](formatters/csproj.md). |
| Configuration | At least one of `pnfmt_sort_entries`, `pnfmt_ini_group_by_prefix`, `pnfmt_ini_merge_groups`, or `pnfmt_ini_sort_groups` is `true`. |
| Resource | Presence of `pnfmt_sort_entries`, `pnfmt_resx_remove_xsd_schema`, or `pnfmt_resx_remove_documentation_comment`, including `false`. The comparer, layout, and charset settings alone do not activate it. |
| Response, solution | `pnfmt_sort_entries = true`. |
| XML | `pnfmt_xml_format = true`. |
| XAML | `pnfmt_xaml_format = true`. |

Legacy project and resource setting names remain accepted as fallbacks. `charset` alone never activates a formatter. A setting removed by EditorConfig's `unset` is treated as absent.

For enabled, nonempty files that are rewritten, `insert_final_newline` has these effects:

| Formatter | `true` | `false` | Missing |
| --- | --- | --- | --- |
| C#, XML, XAML | Add a missing final newline. | Preserve whether one existed. | Preserve whether one existed. |
| Project | End with one newline. | Remove the final newline. | End with one newline. |
| Resource | End with one newline. | Remove the final newline. | Omit the final newline. |
| Configuration, response, solution | End with one newline. | End with one newline. | End with one newline. |

C# exclusion regions and protected XML content can prevent a requested whitespace change; their preservation rules still apply. Legacy resource formatting without explicit layout or charset settings rewrites only when resource sorting or schema/documentation changes are needed.

Configuration, response, and solution formatters use the input's detected line ending and do not apply `end_of_line` or indentation settings. The solution formatter uses two spaces for known XML containers. C#, project, resource, XML, and XAML formatters support explicit layout settings as described on their individual pages.

[Back to the overview](../README.md)
