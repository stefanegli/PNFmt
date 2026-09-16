# Solution formatter (`.slnx`)

The solution formatter normalizes XML layout and orders known elements in XML solution files.

## Configuration

```ini
[*.slnx]
pnfmt_formatter = slnx
```

Select this formatter with `pnfmt_formatter = slnx`; use `None` to disable it. Sorting is part of the selected formatter's behavior. Without a selection, `pnfmt_sort_entries = true` still activates formatting for this version and prints warning `PNFMT004`. See the [activation rules](../configuration-contracts.md).

## Behavior

PNFmt uses two-space XML indentation and orders recognized solution elements consistently. Unknown extension elements remain in place as ordering barriers so known elements are not moved across content owned by extensions.

Extension subtrees retain their character data, including whitespace-only values and carriage-return character references. Indentation is added only inside known solution containers. Containers with character data or an inherited `xml:space="preserve"` keep their complete subtree, including element order.

[Back to the overview](../../README.md)
