# Project formatter (`.csproj`)

The project formatter normalizes MSBuild project XML, sorts eligible properties and items, and keeps evaluation-sensitive content in its original order. Its `--lint` diagnostics identify project-structure issues; the C#, RESX, XML, and XAML formatters also report diagnostics.

Both SDK-style and non-SDK-style projects are supported, including legacy projects using the MSBuild XML namespace. The document must have a `Project` root element; an `Sdk` declaration is not required. Existing namespaces, `ToolsVersion`, imports, and target definitions are preserved.

## Configuration

```ini
[*.csproj]
pnfmt_enabled = true
pnfmt_formatter = csproj
end_of_line = crlf
indent_style = space
pnfmt_csproj_empty_lines_between_groups = 1
pnfmt_sort_entries = true
tab_width = 4
```

| Setting | Description |
| --- | --- |
| `pnfmt_sort_entries` | Sort eligible properties and items when set to `true`. |
| `pnfmt_csproj_empty_lines_between_groups` | Set the non-negative number of empty lines between top-level groups. |
| `pnfmt_csproj_sort_item_types` | Replace the built-in list of sortable item types. Separate names with commas or semicolons, or use `*` for any homogeneous item type. |
| `indent_style`, `indent_size`, `tab_width`, `end_of_line` | Control standard XML layout through EditorConfig. |
| `insert_final_newline` | End the file with one configured newline when `true` (the default), or omit the final newline when `false`. |

Omit `pnfmt_csproj_sort_item_types` to use the built-in item-type list.

Enable processing with `pnfmt_enabled = true` and select `pnfmt_formatter = csproj`. Use `pnfmt_enabled = false` to disable both formatting and linting. Layout defaults on; `pnfmt_format = false` disables layout while allowing sorting or linting. Sorting defaults off and requires `pnfmt_sort_entries = true`. Parameter settings such as group spacing do not enable a behavior. See [activation and compatibility rules](../configuration-contracts.md).

For other MSBuild files, select the same formatter explicitly in a matching EditorConfig section, for example:

```ini
[*.{props,targets,proj}]
pnfmt_enabled = true
pnfmt_formatter = csproj
pnfmt_sort_entries = false
```

This enables layout formatting without sorting. Set `pnfmt_sort_entries = true` only when the consuming build does not depend on the order of sortable items. Custom tasks can observe item order even when the item declarations have no visible dependencies.

## Behavior

### Optional attribute wrapping

Set `xml_wrap_tags_and_pi = true` with `max_line_length` to wrap long project tags between complete attributes. `xml_attribute_style` can instead arrange attributes explicitly, with indentation controlled by `xml_attribute_indent`. These options require layout, default off, and also work for explicitly selected `.props`, `.targets`, and `.proj` files. Conditions, task arguments, and other attribute values remain whole. Wrapping runs after existing serialization and sorting; it does not restore original attribute layout discarded by serialization. See [XML and MSBuild attribute wrapping](xml-wrapping.md) for supported values and aliases.

### Sorting

Property and item sorting is deliberately limited to content that can be reordered without changing normal MSBuild evaluation. Forward property references, item operations, imports, conditions, and other evaluation-sensitive constructs retain their meaningful order.

Property groups containing property functions, member access, or nested property expansions retain their original order because those dependencies cannot be resolved reliably without evaluating the project.

Item groups containing item or metadata references in any item value or attribute, including conditions and metadata, also retain their original item order.

Groups with property expressions, globs, lists, or escaped characters in `Include` retain their item order because distinct expressions can resolve to the same item. Metadata attributes containing references also retain their order.

Sorting applies only to immediate `PropertyGroup` and `ItemGroup` children of `Project`. Imports, targets, task execution order, and groups nested inside targets or `Choose` blocks retain their order. The implicit-default-item diagnostic (`CSPROJ005`) applies only to projects that declare the .NET SDK, not to ordinary non-SDK projects.

Run `pnfmt --lint <paths>` to report project-structure diagnostics and formatting changes without writing files. The command returns exit code `1` when it finds either.

## Regression coverage

The snapshot suite covers the minimal namespaced `LegacyNonSdk.csproj` and the [NonSdk fixtures](../../PNFmt.Tests/Formatter/CsProj/_files/input/NonSdk): a legacy VSIX project, a namespace-free build project, and imported `.props` and `.targets` files. It checks preview behavior, expected formatted output, and idempotence; skipped files fail the project snapshot tests. The non-SDK fixtures also run through the CLI integration tests.

`NonSdk/ResxFormatter.csproj` is copied from the ResxFormatter repository at commit `6b503ad5f714982ff3a892152c10a3f083e4ee85`, immediately before the SDK migration in `cf968ad`. It retains the original MSBuild namespace, conditional build configurations, explicit source files, VSIX metadata, and imports. The tests format this stored copy without building the VSIX project or resolving its Visual Studio dependencies.

[Back to the overview](../../README.md)
