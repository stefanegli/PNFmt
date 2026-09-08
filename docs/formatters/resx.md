# Resource formatter (`.resx`)

The resource formatter normalizes RESX XML, sorts resource entries, and can remove generated schema and documentation content.

## Configuration

```ini
[*.resx]
pnfmt_resx_remove_documentation_comment = true
pnfmt_resx_remove_xsd_schema = true
pnfmt_resx_sort_comparer = OrdinalIgnoreCase
pnfmt_sort_entries = true
```

| Setting | Description |
| --- | --- |
| `pnfmt_sort_entries` | Sort resource entries when set to `true`. |
| `pnfmt_resx_remove_xsd_schema` | Remove the embedded XSD schema when set to `true`. |
| `pnfmt_resx_remove_documentation_comment` | Remove the standard generated documentation comment when set to `true`. |
| `pnfmt_resx_sort_comparer` | Select `InvariantCulture`, `InvariantCultureIgnoreCase`, `OrdinalIgnoreCase`, or `Ordinal`. |
| `indent_style`, `indent_size`, `tab_width`, `end_of_line` | Control standard XML layout through EditorConfig. |
| `insert_final_newline` | End the file with one configured newline when `true`, or omit the final newline when `false` (the legacy default). |

## Behavior

Sorting applies to resource data and metadata entries while retaining comments and other meaningful XML content. Schema and documentation removal are independent options; they do not need entry sorting to be enabled.

Layout settings apply whenever resource formatting is enabled, including when entries are already sorted. Standard layout settings alone do not activate the resource formatter. Without layout settings, existing legacy behavior is retained: files are only rewritten for sorting or schema/documentation changes. When rewriting, the defaults are two-space indentation, the platform line ending, and no final newline. An explicit `indent_size` takes precedence over `tab_width`; tab indentation uses one tab per level.

Line-ending settings control XML layout. Line breaks and significant whitespace inside resource values retain their parsed XML values.

[Back to the overview](../../README.md)
