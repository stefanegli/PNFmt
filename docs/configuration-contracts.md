# Formatter activation and final newlines

Select a formatter in the applicable `.editorconfig` section:

```ini
[*.csproj]
pnfmt_formatter = csproj
pnfmt_sort_entries = true

[*.config]
pnfmt_formatter = xml

[Generated/**]
pnfmt_formatter = None
```

Supported names are `csharp`, `csproj`, `ini`, `resx`, `rsp`, `slnx`, `xml`, and `xaml`. Names and `None` are case-insensitive. The selected formatter runs independently of the file extension and the old activation switches. Optional cleanup and layout settings still control its behavior. RSP and SLNX sorting is built into their formatters. Unknown or empty names are configuration errors; they never fall back to extension-based selection.

`None` skips formatting, including project linting. Normal EditorConfig inheritance applies, so a more specific section can override a choice. `unset` removes an inherited selection. The CLI `--formatter` option filters the resolved formatter names and does not enable formatting or override `None`.

For this version only, a missing selection (including `unset`) retains extension-based selection and the activation rules below. Implicit activation prints warning `PNFMT004` with the setting needed to migrate. The warning appears without `--verbose` and does not change exit codes. A future version will skip files with no selection. `--write-default-config` writes explicit selections and preserves existing selections, including `None`.

| Formatter | Legacy activation when `pnfmt_formatter` is missing |
| --- | --- |
| C# | `pnfmt_csharp_format = true`. Other cleanup switches alone do not activate it. |
| Project | A project-specific setting or standard layout setting. `pnfmt_sort_entries = false` disables sorting while allowing XML layout formatting. See the [project settings](formatters/csproj.md). |
| Configuration | At least one of `pnfmt_sort_entries`, `pnfmt_ini_group_by_prefix`, `pnfmt_ini_merge_groups`, or `pnfmt_ini_sort_groups` is `true`. |
| Resource | Presence of `pnfmt_sort_entries`, `pnfmt_resx_remove_xsd_schema`, or `pnfmt_resx_remove_documentation_comment`, including `false`. The comparer, layout, and charset settings alone do not activate it. |
| Response, solution | `pnfmt_sort_entries = true`. |
| XML | `pnfmt_xml_format = true`. |
| XAML | `pnfmt_xaml_format = true`. |

Legacy project and resource setting names remain accepted as fallbacks. `charset` alone never activates a formatter. A setting removed by EditorConfig's `unset` is treated as absent. Project `--lint` also retains its legacy activation when no formatter is selected, with the same warning.

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
