# GitHub Actions ownership

Keep workflow responsibilities explicit when editing `workflows/`:

- `build.yml` runs on PRs and selected branch pushes with read-only repository
  permissions. It packs the project version once, validates that exact package
  on Windows and Linux, and retains `release-package` for 30 days. It never tags
  or publishes. Both platform job names are required GitHub checks.
- `publish-global-tool.yml` runs only for version-tag pushes. It requires a
  successful default-branch push Build for the exact commit and a retained
  artifact, then checks package identity, version, and source before requesting
  NuGet trusted-publishing credentials. It uploads the existing package without
  compilation, tests, packing, or benchmarks.

Keep the publishing workflow filename and `nuget` environment stable: NuGet
trusted publishing is configured against them. Do not add publication to Build,
accept PR artifacts for releases, fall back to rebuilding in Publish, grant Build
write/OIDC permissions, or remove required platform checks without migrating the
repository rules and release-artifact handoff together.

Shared checks and release selection belong in `build/`. Forge-only tagging code
belongs in `.forge/scripts/`. The current Windows validation overlap is deliberate:
Forge supplies controlled-machine timing enforcement, while GitHub's required
platform jobs validate the exact package used by its publisher. Forge's validation
package cannot silently replace that artifact. See [releases](../docs/releases.md).
