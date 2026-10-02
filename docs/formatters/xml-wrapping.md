# XML and MSBuild attribute wrapping and tag spacing

The `xml`, `xaml`, and `csproj` formatters share optional attribute wrapping, arrangement, and tag spacing. The project formatter also supports `.props`, `.targets`, and `.proj` files when selected explicitly. These options follow [JetBrains XML property names](https://www.jetbrains.com/help/resharper/EditorConfig_XML_XmlCodeStylePageSchema.html).

Wrapping and spacing normalization are disabled by default; none of these properties are added by `--write-default-config`. Layout must be enabled: `pnfmt_format = false` disables this pass. These settings alone do not activate file processing.

## Wrapping long lines

```ini
[*.xml]
pnfmt_enabled = true
pnfmt_formatter = xml
max_line_length = 120
xml_wrap_tags_and_pi = true

[*.{csproj,props,targets,proj}]
pnfmt_enabled = true
pnfmt_formatter = csproj
max_line_length = 120
xml_wrap_tags_and_pi = true
```

Set both `xml_wrap_tags_and_pi = true` and a positive `max_line_length` to add breaks before attributes that exceed the target width. Complete attributes are kept together, with as many fitting on a line as possible. Width includes indentation, tab stops, and the closing delimiter when it follows the last attribute. A line exactly at the width is retained. The suggested 120 columns is a policy choice, not a built-in default.

`xml_max_line_length` overrides the general width for these formatters. Missing, `unset`, zero, negative, or invalid widths disable width-based wrapping after fallback resolution. A missing, false, or invalid wrapping switch disables width-based wrapping; explicit attribute styles can still apply without it or a width.

Wrapping changes only whitespace before complete attributes; the separate spacing options below can normalize other tag whitespace. A long element name or attribute value may exceed the target. Wrapping does not split MSBuild conditions, paths, task arguments, or other values, nor wrap element text, comments, or CDATA. Ordinary processing-instruction data is opaque and remains exact; the XML declaration can wrap between its pseudo-attributes.

## Attribute arrangement and indentation

`xml_attribute_style` selects an arrangement independently of width:

| Value | Behavior |
| --- | --- |
| `on_single_line` | Replace whitespace between the tag name and attributes with single spaces. Width wrapping can then add breaks if enabled. |
| `first_attribute_on_single_line` | Keep the first attribute after the tag name and put each subsequent attribute on its own line. Width wrapping can also move the first attribute. |
| `on_different_lines` | Put every attribute on its own line after the tag name, including a single attribute. |
| `do_not_touch` | Leave element-tag attribute arrangement unchanged by this pass, even when width wrapping is enabled. XML declaration wrapping is independent. |

When the style is missing or invalid, existing attribute breaks are retained and enabled width wrapping only adds breaks. Empty attribute lists remain empty. Whitespace before `>` or `/>` retains its existing spelling unless explicitly configured below; an existing closing-delimiter line break is always retained.

`xml_attribute_indent` controls new continuation indentation:

| Value | Behavior |
| --- | --- |
| `single_indent` | One level deeper than the element. This is the default. |
| `double_indent` | Two levels deeper than the element. |
| `align_by_first_attribute` | Align subsequent attribute lines with the first attribute's actual column, using spaces. If the first attribute moves to its own line, start it one level deeper. |

Invalid indentation styles use `single_indent`. This parameter alone does not activate rearrangement. New indentation follows the formatter's `indent_style` and `indent_size`; newlines follow its configured or detected convention. Tab measurement uses a positive `tab_width` up to 256; missing or invalid values use the formatter's resolved indentation width. XML declaration continuations use one indentation level and do not use the element attribute style or alignment choice.

For example, configure `xml_attribute_style = on_different_lines` and `xml_attribute_indent = single_indent` to produce:

```xml
<PackageReference
    Include="Some.Long.Package.Name"
    Version="1.2.3"
    PrivateAssets="all" />
```

## Tag spacing

These independent options accept `true` or `false`. Missing, invalid, or `unset` values preserve the corresponding source whitespace after alias resolution. No width or wrapping switch is required.

| Setting | `true` | `false` |
| --- | --- | --- |
| `xml_spaces_around_eq_in_attribute` | One space on each side: `Name = 'value'`. | No spaces: `Name='value'`. |
| `xml_space_after_last_attribute` | One space before `>` in a non-self-closing start tag with attributes. | No space before `>`. |
| `xml_space_before_self_closing` | One space before `/>`, including tags without attributes. | No space before `/>`. |

Spacing around `=` can replace line breaks outside the quoted value. Delimiter spacing changes only horizontal whitespace; existing line breaks and their indentation before `>` or `/>` are preserved. End tags, XML declarations, and ordinary processing instructions are not affected by these options. `xml_attribute_style = do_not_touch` disables arrangement and width wrapping for element tags but still permits explicitly selected spacing changes. Spacing is normalized before measuring the wrapping width.

For example, this configuration turns `<Entry  First = 'one'    Second = 'two'/>` into `<Entry First='one' Second='two' />`:

```ini
[*.xml]
pnfmt_enabled = true
pnfmt_formatter = xml
xml_attribute_style = on_single_line
xml_spaces_around_eq_in_attribute = false
xml_space_after_last_attribute = false
xml_space_before_self_closing = true
```

Quote style is preserved. There is no quote-normalization setting.

## Aliases and preservation

The options are case-insensitive. Every `xml_` option on this page supports a `resharper_` prefix alias, including `resharper_xml_spaces_around_eq_in_attribute`, `resharper_xml_space_after_last_attribute`, and `resharper_xml_space_before_self_closing`. The name without `resharper_` wins when present, including invalid values. `unset` removes that spelling and allows an alias to apply. Width precedence is `xml_max_line_length`, its prefixed alias, then `max_line_length`. Unqualified JetBrains aliases such as `attribute_style` are not supported.

The pass leaves attribute order, names, quotes, entity spellings, and values intact. Text, CDATA, and all content whitespace in text-bearing, mixed-content, and XAML text-container subtrees remain exact. Start-tag attributes throughout those subtrees can wrap or normalize spacing without inserting separators between inline elements or changing text indentation. Whitespace-only leaf values also remain exact. Width is a target for tag attributes, not a limit on preserved text following a start tag.

`xml:space="preserve"` protects the complete subtree, including tag spelling. Nested `xml:space="default"` does not override an already protected subtree. XAML structural and custom tags outside these explicit preservation boundaries can have their attributes formatted without changing their child-content layout.

For MSBuild, the existing project serializer and any enabled sorting run first. They retain their established normalization and canonicalization behavior; this tag-formatting pass operates on that result. Consequently, `do_not_touch` prevents additional attribute arrangement changes but does not make MSBuild preserve the original tag spelling or manual attribute layout. Explicit spacing options can still apply. Project documents containing a DTD retain existing serializer behavior and skip the pass.

Explicit charset changes are reflected in the XML declaration before wrapping, including when a declaration must be inserted, so the result is stable on the first run. Preview, check, lint, encoding, and final-newline behavior remain those of the selected formatter. See [XML/XAML](xml-xaml.md), [MSBuild](csproj.md), and [configuration contracts](../configuration-contracts.md).
