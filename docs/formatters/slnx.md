# Solution formatter (`.slnx`)

The solution formatter normalizes XML layout and orders known elements in XML solution files.

## Configuration

```ini
[*.slnx]
pnfmt_sort_entries = true
```

Only `pnfmt_sort_entries = true` activates this formatter. Missing, invalid, and `false` values leave the file unchanged.

## Behavior

PNFmt uses two-space XML indentation and orders recognized solution elements consistently. Unknown extension elements remain in place as ordering barriers so known elements are not moved across content owned by extensions.

[Back to the overview](../../README.md)
