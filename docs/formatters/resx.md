# Resource formatter (`.resx`)

The resource formatter normalizes RESX XML, sorts resource entries, and can remove generated schema and documentation content.

## Configuration

```ini
[*.resx]
pnfmt_formatter = resx
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
| `charset` | Choose the output encoding and BOM. `utf-8` writes without a BOM; `utf-8-bom` writes with one. Also supports `utf-16le`, `utf-16be`, and `latin1`. |

Select this formatter with `pnfmt_formatter = resx`; use `None` to disable it. Without a selection, the previous activation rules remain for this version and print warning `PNFMT004`. See the [activation rules](../configuration-contracts.md).

## Behavior

Sorting applies to resource data and metadata entries while retaining comments and other meaningful XML content. Schema and documentation removal are independent options; they do not need entry sorting to be enabled.

XML comments immediately preceding a resource entry travel with that entry when sorting. Comments preceding headers or schema stay with that content, and trailing comments remain after the last resource. The standard generated documentation comment is handled by its own setting.

Layout and charset settings apply whenever resource formatting is enabled, including when entries are already sorted. These standard settings alone do not activate the resource formatter. Without layout or charset settings, existing legacy behavior is retained: files are only rewritten for sorting or schema/documentation changes. When rewriting, the defaults are two-space indentation, the platform line ending, and no final newline. An explicit `indent_size` takes precedence over `tab_width`; tab indentation uses one tab per level.

An explicit `charset` takes precedence over the XML declaration's encoding, and the declaration is updated to match. Without a supported charset, serialization uses the declared encoding or defaults to UTF-8 with a BOM. See [file encoding](../../README.md#file-encoding) for the shared rules.

Line-ending settings control XML layout. Line breaks and significant whitespace inside resource values retain their parsed XML values.

Well-formed XML that is not a RESX resource document, or contains an unnamed data or metadata entry, is skipped without rewriting and reports `RESX001`. The diagnostic appears in all modes; `--lint` returns exit code 1. Malformed XML remains an execution error with exit code 2.

[Back to the overview](../../README.md)
