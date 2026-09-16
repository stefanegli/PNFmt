# XML and XAML formatters

PNFmt formats `.xml` and `.xaml` files independently, without loading schemas, application types, projects, or solutions. These are indentation formatters: element and attribute order, namespace prefixes, and attribute layout stay unchanged. Existing `.csproj`, `.resx`, and `.slnx` files continue to use their dedicated formatters.

## Configuration

```ini
[*.xml]
pnfmt_formatter = xml
indent_size = 2
indent_style = space

[*.xaml]
pnfmt_formatter = xaml
indent_size = 4
indent_style = space
```

Select a formatter with `pnfmt_formatter = xml` or `pnfmt_formatter = xaml`; use `None` to disable it. Without a selection, `pnfmt_xml_format = true` and `pnfmt_xaml_format = true` still activate their respective formatters for this version and print warning `PNFMT004`. `pnfmt_sort_entries` never enables sorting for these formatters. `--write-default-config` adds explicit selections in their respective sections, preserving existing choices. See the [activation rules](../configuration-contracts.md).

```powershell
pnfmt --formatter xml,xaml --recursive .
pnfmt --formatter xml,xaml --check --recursive .
```

The normal Git changed-file selection, `--all`, `--file-pattern`, `--dry-run`, and parallel processing options apply.

## Layout settings

| Setting | Behavior |
| --- | --- |
| `indent_style` | `space` or `tab`; defaults to spaces. |
| `indent_size` | Spaces per level; defaults to 4. Positive values up to 256 are accepted. Invalid values use the default. `tab` uses `tab_width`. |
| `tab_width` | Used when `indent_size = tab`; defaults to 4. Tab indentation emits one tab per level. |
| `end_of_line` | `lf`, `crlf`, or `cr`; otherwise uses the detected file convention. |
| `insert_final_newline` | `true` adds a final newline. Otherwise the presence of a final newline is preserved. |

Formatting places structural child elements, comments, and processing instructions on separate indented lines and normalizes whitespace in those gaps. The XML declaration, top-level comments, processing instructions, and root element occupy separate lines. Excess layout blank lines are removed.

Start and end tags retain their exact original text, including quote style, spaces around attributes, multiline attribute layout, entity references, and self-closing tag spelling. Comments, processing instructions, and CDATA retain their contents. Line endings inside these protected parts are not normalized. Global trailing-whitespace cleanup and line-length wrapping are not applied.

## Text preservation

An element containing character data or CDATA keeps its entire subtree unchanged. This includes mixed content such as `<p>Hello <b>world</b>!</p>`, numeric character references, and whitespace-only leaf values. `xml:space="preserve"` protects the complete subtree, even if a descendant requests `xml:space="default"`.

For ordinary XML, enabling formatting treats whitespace-only gaps around child markup as layout. Applications that interpret such gaps as data should use `xml:space="preserve"` or leave formatting disabled for those files. No schema-based determination of insignificant whitespace is attempted.

## Additional XAML rules

XAML can treat spaces between inline elements as visible text. PNFmt therefore preserves entire text-oriented subtrees such as `TextBlock`, `TextBox`, `RichTextBox`, `Span`, `Run`, `Bold`, `Italic`, `Underline`, `Hyperlink`, `Paragraph`, `Section`, `FlowDocument`, `FormattedString`, and `String`, as well as property elements ending in `.Inlines`.

Child indentation is enabled only for a fixed set of common structural types in the WPF/WinUI presentation, Avalonia, and MAUI namespaces. This includes windows/pages, grids/panels, resource dictionaries, styles, templates, triggers, and visual states. Selected structural property elements, such as `Grid.RowDefinitions` and `ResourceDictionary.MergedDictionaries`, are supported too. The full lists are in [XamlWhitespacePolicy.cs](../../PNFmt.Core/Formatter/Xml/XamlWhitespacePolicy.cs).

Unknown and custom elements retain their own whitespace. Known structural descendants may still format, provided they are outside a protected text or `xml:space` subtree. A compact custom root may therefore remain partly compact. XAML with no recognized namespace also retains its container whitespace.

Resource dictionary order, setter order, bindings, markup extensions, and namespace declarations remain unchanged. PNFmt checks XML well-formedness, not whether the XAML types or resources resolve at runtime.

## Encoding and diagnostics

Both formatters honor the shared [EditorConfig charset settings](../../README.md#file-encoding), updating the XML declaration when choosing an output encoding. Without a supported charset, they preserve UTF-8 with or without a BOM and BOM-marked UTF-16/UTF-32 in either byte order, retaining the XML declaration exactly. With an explicit charset, input BOMs and XML encoding declarations are respected, including Latin-1 declarations. Undecodable files are reported as errors without rewriting them.

Malformed XML, documents containing a DTD, and nesting beyond 256 levels are skipped with `XML001` or `XAML001`. External entities and schemas are never loaded. Skipped files remain unchanged, and diagnostics include a line number when available.

Diagnostics appear in all modes. `--lint` returns exit code 1 when they occur. A skipped file alone does not fail ordinary formatting or `--check`; `--check` fails when formatting changes are needed. Preview modes never write files.

[Back to the overview](../../README.md)
