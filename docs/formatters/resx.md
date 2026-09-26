# Resource formatter (`.resx`)

The resource formatter normalizes RESX XML, sorts resource entries, and can remove generated schema and documentation content.

## Configuration

```ini
[*.resx]
pnfmt_enabled = true
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
| `pnfmt_resx_insert_xsd_schema` | Insert missing schema when `true`; defaults to `false` in explicit configurations. |
| `pnfmt_resx_insert_documentation_comment` | Insert missing documentation when `true`; defaults to `false` in explicit configurations. |
| `pnfmt_resx_sort_comparer` | Select `InvariantCulture`, `InvariantCultureIgnoreCase`, `OrdinalIgnoreCase`, or `Ordinal`. |
| `indent_style`, `indent_size`, `tab_width`, `end_of_line` | Control standard XML layout through EditorConfig. |
| `insert_final_newline` | End the file with one configured newline when `true`, or omit the final newline when `false` (the legacy default). |
| `charset` | Choose the output encoding and BOM. `utf-8` writes without a BOM; `utf-8-bom` writes with one. Also supports `utf-16le`, `utf-16be`, and `latin1`. |

Enable processing with `pnfmt_enabled = true` and select `pnfmt_formatter = resx`. Use `pnfmt_enabled = false` to disable it while retaining its options. Layout defaults on; `pnfmt_format = false` disables layout while allowing independently enabled sorting, removal, or insertion. Those optional transformations default off. Removal takes precedence if both removal and insertion are enabled for the same content. See [activation and compatibility rules](../configuration-contracts.md).

## Behavior

Sorting applies to resource data and metadata entries while retaining comments and other meaningful XML content. Schema and documentation removal are independent options; they do not need entry sorting to be enabled.

XML comments immediately preceding a resource entry travel with that entry when sorting. Comments preceding headers or schema stay with that content, and trailing comments remain after the last resource. The standard generated documentation comment is handled by its own setting.

Layout settings apply when `pnfmt_format` is on, including when entries are already sorted. An explicit `charset` can also request an encoding change independently of layout. With both activation controls missing, the legacy behavior remains: files without layout or charset settings are only rewritten for sorting or schema/documentation changes. Legacy configurations can insert missing schema or documentation when removal is off; explicit configurations require the insertion switches. Layout defaults are two-space indentation, the platform line ending, and no final newline. An explicit `indent_size` takes precedence over `tab_width`; tab indentation uses one tab per level.

An explicit `charset` takes precedence over the XML declaration's encoding, and the declaration is updated to match. Without a supported charset, serialization uses the declared encoding or defaults to UTF-8 with a BOM. See [file encoding](../../README.md#file-encoding) for the shared rules.

With layout enabled, `end_of_line = lf`, `crlf`, or `cr` controls XML layout, XML comments, and processing instructions. The platform line ending applies when the setting is omitted. Match Git's checkout line-ending policy to this setting for stable checks.

Resource strings retain the exact characters read by .NET's resource readers, including LF, CR, CRLF, and significant whitespace. Serialization writes text newlines as `&#xA;` and `&#xD;`, protecting values, resource `<comment>` text, and metadata from Git checkout conversion. For example, a value containing `first` + LF + `second` is written as `<value>first&#xA;second</value>` even with CRLF layout. CDATA containing newlines becomes escaped text with character references; its value is preserved, but its CDATA spelling is not. This protection also applies when sorting or other transformations cause serialization with `pnfmt_format = false`; that setting disables layout normalization, not resource-value preservation. Encoding-only changes without content or layout changes retain the existing text representation.

This corrects earlier behavior that could normalize physical CR/CRLF resource text to LF. A normalizing `XDocument.Load(path)` comparison cannot validate runtime string preservation: it hides newline differences that .NET resource readers and compiled resources retain. Formatting cannot recover characters already changed by an earlier formatter or Git checkout.

Well-formed XML that is not a RESX resource document, or contains an unnamed data or metadata entry, is skipped without rewriting and reports `RESX001`. The diagnostic appears in all modes; `--lint` returns exit code 1. Malformed XML remains an execution error with exit code 2.

[Back to the overview](../../README.md)
