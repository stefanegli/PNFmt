# Test ownership

Keep one primary owner for each contract. Shared charset, configuration and file
replacement suites own behavior common to the formatters. Approved formatter
snapshots own complete canonical output and their harness owns preview/write
idempotence. Keep another layer when it protects a distinct risk, such as CLI
dispatch, semantic preservation, configuration precedence or physical newline bytes.

Expected output must be independent of the renderer under test. Comparing CLI
delivery with library output is appropriate when approved snapshots separately own
the rendering contract. Order assertions must require every expected item and use
complete identifiers; prefer exact parsed sequences over substring positions.

Before removing overlapping coverage, identify the keeper and carry any unique
assertions or parameter rows into it. Preserve snapshot identities when consolidating
setup, including configuration inheritance, culture and same-instance scenarios.
Use scoped temporary directories and restore any culture or process state changed
by a test. Do not retain production constructors or helpers solely for tests.
