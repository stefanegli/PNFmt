# Response-file formatter (`.rsp`)

The response-file formatter sorts arguments stored as physical lines in .NET compiler response files.

## Configuration

```ini
[*.rsp]
pnfmt_enabled = true
pnfmt_formatter = rsp
pnfmt_sort_entries = true
```

Enable processing with `pnfmt_enabled = true` and select `pnfmt_formatter = rsp`. Use `pnfmt_enabled = false` to disable it while retaining its options. Layout defaults on; sorting defaults off and requires `pnfmt_sort_entries = true`. Set `pnfmt_format = false` to sort without normalizing whitespace or final newlines. See [activation and compatibility rules](../configuration-contracts.md).

## Behavior

When sorting is enabled, PNFmt sorts non-empty physical lines using ordinal, case-insensitive comparison with an ordinal tie-breaker. It does not tokenize a line or rearrange multiple arguments within it.

Blank lines do not divide sortable content. A line whose first non-whitespace character is `#` acts as a barrier and remains in place, preventing entries from moving across it.

Compiler response arguments can be order-sensitive because later options may override earlier ones. Enable sorting only when changing line order is safe. Add a full-line `#` comment between order-sensitive blocks when their relative placement must be preserved.

[Back to the overview](../../README.md)
