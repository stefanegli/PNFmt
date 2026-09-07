# Response-file formatter (`.rsp`)

The response-file formatter sorts arguments stored as physical lines in .NET compiler response files.

## Configuration

```ini
[*.rsp]
pnfmt_sort_entries = true
```

Only `pnfmt_sort_entries = true` activates this formatter. Missing, invalid, and `false` values leave the file unchanged.

## Behavior

PNFmt sorts non-empty physical lines using ordinal, case-insensitive comparison with an ordinal tie-breaker. It does not tokenize a line or rearrange multiple arguments within it.

Blank lines do not divide sortable content. A line whose first non-whitespace character is `#` acts as a barrier and remains in place, preventing entries from moving across it.

Compiler response arguments can be order-sensitive because later options may override earlier ones. Enable sorting only when changing line order is safe. Add a full-line `#` comment between order-sensitive blocks when their relative placement must be preserved.

[Back to the overview](../../README.md)
