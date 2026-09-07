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

## Behavior

Sorting applies to resource data and metadata entries while retaining comments and other meaningful XML content. Schema and documentation removal are independent options; they do not need entry sorting to be enabled.

[Back to the overview](../../README.md)
