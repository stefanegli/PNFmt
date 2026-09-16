# Response-file formatter (`.rsp`)

The response-file formatter sorts arguments stored as physical lines in .NET compiler response files.

## Configuration

```ini
[*.rsp]
pnfmt_formatter = rsp
```

Select this formatter with `pnfmt_formatter = rsp`; use `None` to disable it. Sorting is part of the selected formatter's behavior. Without a selection, `pnfmt_sort_entries = true` still activates formatting for this version and prints warning `PNFMT004`. See the [activation rules](../configuration-contracts.md).

## Behavior

PNFmt sorts non-empty physical lines using ordinal, case-insensitive comparison with an ordinal tie-breaker. It does not tokenize a line or rearrange multiple arguments within it.

Blank lines do not divide sortable content. A line whose first non-whitespace character is `#` acts as a barrier and remains in place, preventing entries from moving across it.

Compiler response arguments can be order-sensitive because later options may override earlier ones. Enable sorting only when changing line order is safe. Add a full-line `#` comment between order-sensitive blocks when their relative placement must be preserved.

[Back to the overview](../../README.md)
