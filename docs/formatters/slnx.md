# Solution formatter (`.slnx`)

The solution formatter normalizes XML layout and orders known elements in XML solution files.

## Configuration

```ini
[*.slnx]
pnfmt_enabled = true
pnfmt_formatter = slnx
pnfmt_sort_entries = true
```

Enable processing with `pnfmt_enabled = true` and select `pnfmt_formatter = slnx`. Use `pnfmt_enabled = false` to disable it while retaining its options. Layout defaults on; sorting defaults off and requires `pnfmt_sort_entries = true`. Set `pnfmt_format = false` to sort without re-indenting. See [activation and compatibility rules](../configuration-contracts.md).

## Behavior

Layout uses two-space XML indentation. Sorting, when enabled, orders recognized solution elements consistently. Unknown extension elements remain in place as ordering barriers so known elements are not moved across content owned by extensions.

Extension subtrees retain their character data, including whitespace-only values and carriage-return character references. Indentation is added only inside known solution containers. Containers with character data or an inherited `xml:space="preserve"` keep their complete subtree, including element order.

[Back to the overview](../../README.md)
