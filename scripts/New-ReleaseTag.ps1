[CmdletBinding()]
param(
    [string]$PackageDirectory = 'incoming/validated-package',
    [switch]$CheckOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ($env:PNFORGE_REPOSITORY_ID -cne 'pnfmt' -or $env:PNFORGE_JOB_ID -cne 'release-tag' -or
    $env:PNFORGE_SOURCE_COMMIT -cnotmatch '^[0-9a-f]{40}$' -or
    $env:PNFORGE_BUILD_NUMBER -notmatch '^[1-9][0-9]*$' -or
    $env:PNFORGE_RUN_ID -notmatch '^[0-9a-fA-F-]{36}$')
{
    throw 'Release tagging requires the captured pnfmt release-tag job and its source/run/build identity.'
}

Push-Location $repoRoot
try
{
    # This input is bound by Forge to this run's successful complete validation.
    & (Join-Path $PSScriptRoot 'Test-ReleasePackage.ps1') -PackageDirectory $PackageDirectory `
        -Version '0.0.0-validation.1' -Commit $env:PNFORGE_SOURCE_COMMIT | Out-Null

    $version = (& dotnet msbuild PNFmt.Cli/PNFmt.Cli.csproj -getProperty:Version -nologo | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the release version.' }
    $arguments = @(
        '.github/scripts/create-release-tag.cjs', $env:PNFORGE_SOURCE_COMMIT, $version,
        $env:PNFORGE_BUILD_NUMBER
    )
    if ($CheckOnly) { $arguments += '--check-only' }
    $result = & node @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Release tag checks or creation failed. Inspect the GitHub tag and Publish run before retrying.' }
    $receipt = $result | Out-String | ConvertFrom-Json
    $receipt | Add-Member -NotePropertyName forgeRunId -NotePropertyValue $env:PNFORGE_RUN_ID
    $receipt | Add-Member -NotePropertyName forgeBuildNumber -NotePropertyValue $env:PNFORGE_BUILD_NUMBER
    $outputDirectory = Join-Path $repoRoot 'artifacts/release-tag'
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $outputDirectory 'release-tag.json'),
        ($receipt | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
    Write-Host ($receipt | ConvertTo-Json -Depth 10)
}
finally
{
    Pop-Location
}
