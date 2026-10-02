# XML and XAML formatters

PNFmt formats `.xml` and `.xaml` files independently, without loading schemas, application types, projects, or solutions. It indents structural content and optionally wraps attributes or normalizes tag spacing. Element and attribute order and namespace prefixes stay unchanged. Existing `.csproj`, `.resx`, and `.slnx` files continue to use their dedicated formatters.

## Configuration

```ini
[*.xml]
pnfmt_enabled = true
pnfmt_formatter = xml
indent_size = 2
indent_style = space

[*.xaml]
pnfmt_enabled = true
pnfmt_formatter = xaml
indent_size = 4
indent_style = space
```

Enable processing with `pnfmt_enabled = true` and select `pnfmt_formatter = xml` or `pnfmt_formatter = xaml`. Use `pnfmt_enabled = false` to disable processing while retaining its options. Layout defaults on and can be disabled with `pnfmt_format = false`; the older `pnfmt_xml_format` and `pnfmt_xaml_format` settings remain layout fallbacks. An explicit `charset` can request encoding changes independently of layout. `pnfmt_sort_entries` never enables sorting for these formatters. `--write-default-config` adds explicit enablement and selections, preserving existing choices. See [activation and compatibility rules](../configuration-contracts.md).

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

By default, start and end tags retain their exact original text, including quote style, spaces around attributes, multiline attribute layout, entity references, and self-closing tag spelling. Comments, processing instructions, and CDATA retain their contents. Line endings inside these protected parts are not normalized. Global trailing-whitespace cleanup is not applied.

## Optional attribute wrapping and tag spacing

Set `xml_wrap_tags_and_pi = true` with `max_line_length` to wrap long tags between complete attributes. `xml_attribute_style` also supports explicit single-line or one-attribute-per-line arrangements, and `xml_attribute_indent` controls continuation indentation. The XML declaration can wrap; ordinary processing-instruction data stays intact. Attributes can wrap on text-bearing elements and within mixed-content or XAML text containers while preserving their content whitespace.

`xml_spaces_around_eq_in_attribute`, `xml_space_after_last_attribute`, and `xml_space_before_self_closing` accept `true` or `false` to normalize tag spacing independently of wrapping. Missing/invalid settings preserve existing spacing; existing closing-delimiter line breaks stay intact.

`pnfmt_xml_quote_style` accepts `preserve` (default), `single_quotes`, or `double_quotes`. Missing/invalid/`unset` values preserve quotes. Only conflicting literal quote characters are escaped as `&apos;` or `&quot;`; existing references, literal whitespace, and parsed attribute values remain unchanged. Text, CDATA, comments, declarations, processing instructions, and explicit `xml:space` subtrees are unaffected. All these options require enabled layout and are absent from generated configuration. See [XML and MSBuild attribute formatting](xml-wrapping.md) for aliases, examples, and preservation rules.

## Text preservation

An element containing character data or CDATA preserves text and content whitespace throughout its subtree. This includes mixed content such as `<p>Hello <b>world</b>!</p>`, numeric character references, and whitespace-only leaf values. Opt-in tag formatting can change start-tag whitespace, including on descendants, without adding or removing text separators. With those options omitted, the entire subtree remains unchanged. `xml:space="preserve"` always protects the complete subtree including tag spelling, even if a descendant requests `xml:space="default"`.

For ordinary XML, enabling formatting treats whitespace-only gaps around child markup as layout. Applications that interpret such gaps as data should use `xml:space="preserve"` or leave formatting disabled for those files. No schema-based determination of insignificant whitespace is attempted.

## Additional XAML rules

XAML can treat spaces between inline elements as visible text. PNFmt therefore preserves content whitespace throughout text-oriented subtrees such as `TextBlock`, `TextBox`, `RichTextBox`, `Span`, `Run`, `Bold`, `Italic`, `Underline`, `Hyperlink`, `Paragraph`, `Section`, `FlowDocument`, `FormattedString`, and `String`, as well as property elements ending in `.Inlines`. Their start-tag attributes can still use explicitly configured wrapping or spacing.

Child indentation is enabled only for a fixed set of common structural types in the WPF/WinUI presentation, Avalonia, and MAUI namespaces. This includes windows/pages, grids/panels, resource dictionaries, styles, templates, triggers, and visual states. Selected structural property elements, such as `Grid.RowDefinitions` and `ResourceDictionary.MergedDictionaries`, are supported too. The full lists are in [XamlWhitespacePolicy.cs](../../PNFmt.Core/Formatter/Xml/XamlWhitespacePolicy.cs).

Unknown and custom elements retain their own whitespace. Known structural descendants may still format, provided they are outside a protected text or `xml:space` subtree. A compact custom root may therefore remain partly compact. XAML with no recognized namespace also retains its container whitespace.

Resource dictionary order, setter order, bindings, markup extensions, and namespace declarations remain unchanged. PNFmt checks XML well-formedness, not whether the XAML types or resources resolve at runtime.

## Encoding and diagnostics

Both formatters honor the shared [EditorConfig charset settings](../../README.md#file-encoding), updating the XML declaration when choosing an output encoding. Without a supported charset, they preserve UTF-8 with or without a BOM and BOM-marked UTF-16/UTF-32 in either byte order, retaining the XML declaration exactly. With an explicit charset, input BOMs and XML encoding declarations are respected, including Latin-1 declarations. Undecodable files are reported as errors without rewriting them.

Malformed XML, documents containing a DTD, and nesting beyond 256 levels are skipped with `XML001` or `XAML001`. External entities and schemas are never loaded. Skipped files remain unchanged, and diagnostics include a line number when available.

Diagnostics appear in all modes. `--lint` returns exit code 1 when they occur. A skipped file alone does not fail ordinary formatting or `--check`; `--check` fails when formatting changes are needed. Preview modes never write files.

[Back to the overview](../../README.md)
