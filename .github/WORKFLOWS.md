# GitHub Actions ownership

This file is named `WORKFLOWS.md` because GitHub gives a README in `.github/`
precedence over the repository's root README. Keep the product README at the root;
see [GitHub's README selection rules](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-readmes#about-readmes).

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

The publishing workflow defaults to no permissions; only its tag-push job in the
canonical repository receives read access and `id-token: write`. Credentials are
requested after artifact selection and package validation. Keep that ordering.
Checkouts do not persist Git credentials. For permission semantics, see
[GitHub's job permissions reference](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#jobsjob_idpermissions).

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
