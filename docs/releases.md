# Releasing PNFmt

Releases use a version tag on an already validated `master` commit. Do not create
a release branch or push another commit just to trigger publication.

## Prepare through normal development

Set the intended version in `PNFmt.Cli/PNFmt.Cli.csproj` and update the installation
examples in `README.md` as part of the normal contribution flow. Merge the changes
into `master` through the required checks. An ordinary development PR is separate
from the release operation; no temporary `release/*` branch is needed.

Build runs on pull requests and pushes to `master`, rather than on every branch
push as well. It packs the project version once, records the source commit in the
package manifest, and uploads `release-package`. Both platform jobs download that
same package. Each platform must pass formatting, tests, coverage, benchmark
correctness, the full performance comparison, and isolated installed-tool checks.
The required platform checks also fail if the package job fails.

Only a successful **push Build on the default branch for the exact commit** can
supply a release package. PR checks validate proposed changes but cannot supply
the published artifact. This matters because merging can change the commit or
its contents.

## Validate locally and tag

1. Check out the exact release candidate from `master` in a clean working tree.
2. On VELA, run the complete validation with timing enforcement and no skips:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-GlobalTool.ps1 -Version 0.0.0-validation.1 -PackOnly
   ```

3. Wait for that commit's default-branch Build to pass on GitHub. Check that its
   `release-package` artifact is available and the project version is the intended
   new release version. Hosted timing warnings do not replace VELA validation.
4. Create and push only the version tag. For example, with the repository's
   `github` remote and the version read from the project:

   ```powershell
   $releaseVersion = (dotnet msbuild PNFmt.Cli/PNFmt.Cli.csproj -getProperty:Version -nologo | Out-String).Trim()
   if ($LASTEXITCODE -ne 0) { throw 'Could not read the release version.' }
   $releaseTag = "v$releaseVersion"
   git tag -a $releaseTag -m "Release $releaseVersion"
   git push github "refs/tags/$releaseTag"
   ```

Tags must use `v<major>.<minor>.<patch>[-prerelease]`, match the package version,
and point to a commit on the default branch. Never move an existing release tag.

The tag triggers **Publish .NET global tool**, which finds the successful Build,
downloads its exact artifact, and verifies the package ID, version, and source
commit before obtaining NuGet credentials. It then uploads the existing package;
it runs no compilation, tests, packing, or benchmarks. The workflow filename and
`nuget` environment remain unchanged for NuGet trusted publishing.

After publication, verify installation from NuGet and publish the GitHub release
notes for that existing tag. NuGet indexing can take additional time after upload.

## Missing checks, expired artifacts, and retries

The package is retained for 30 days. A failed/incomplete Build, a package for a
different commit or version, a PR artifact, or a missing/expired artifact blocks
publication. There is no fallback that rebuilds or bypasses validation in Publish.

- If Build is still running, wait for success and rerun the failed Publish job.
- If the artifact is expired or missing, use **Re-run all jobs** on the original
  default-branch Build for that commit. This recreates the package and repeats all
  checks together. Then rerun Publish. No new branch or tag is needed.
- If the tag version is wrong, correct the release preparation through normal
  development and choose an unused version/tag. Do not overwrite a published
  package or move its tag.
- If NuGet accepted the package but a later workflow step failed, check NuGet
  before retrying: publication deliberately does not ignore duplicate versions.

Direct `Publish-GlobalTool.ps1` usage retains its complete local validation and
existing switches. `Test-ToolPackage.ps1` only tests an existing package in
isolation; invoking it alone does not approve a release. See the
[performance policy](performance.md) for required VELA checks and reports.
