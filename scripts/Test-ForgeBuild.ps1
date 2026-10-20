[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$validationVersion = '0.0.0-validation.1'

function Invoke-NativeCommand([string]$FilePath, [string[]]$ArgumentList)
{
    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) { throw "'$FilePath' exited with code $LASTEXITCODE." }
}

Push-Location $repoRoot
try
{
    Invoke-NativeCommand 'dotnet' @('restore', 'PNFmt.slnx')
    $formatOutput = @(& dotnet format PNFmt.slnx --verify-no-changes --no-restore --verbosity diagnostic 2>&1)
    $formatExitCode = $LASTEXITCODE
    $formatOutput | ForEach-Object { Write-Host $_ }
    if ($formatExitCode -ne 0 -or
        ($formatOutput -match 'Found project reference without a matching metadata reference:'))
    {
        throw 'dotnet format failed or reported a project reference it could not load.'
    }

    Invoke-NativeCommand 'dotnet' @('build', 'PNFmt.slnx', '--configuration', 'Release', '--no-restore')
    & (Join-Path $PSScriptRoot 'Format-Repository.ps1') -Check -Configuration Release -NoBuild
    & (Join-Path $PSScriptRoot 'Test-CoverageReport.Tests.ps1')
    & (Join-Path $PSScriptRoot 'Test-ReleasePackage.Tests.ps1')
    Invoke-NativeCommand 'node' @('--test', '.github/scripts/select-release-build.test.cjs')
    Invoke-NativeCommand 'dotnet' @(
        'run', '--project', 'benchmarks/PNFmt.Benchmarks.csproj',
        '--configuration', 'Release', '--no-build', '--', '--quick'
    )

    # Use the same complete release gate as local validation, without publication.
    # Keep every test, package check and performance comparison enabled.
    & (Join-Path $PSScriptRoot 'Publish-GlobalTool.ps1') `
        -Version $validationVersion -PackOnly -TimingPolicy Enforce

    $commit = (& git rev-parse HEAD | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the validated source commit.' }
    & (Join-Path $PSScriptRoot 'Test-ReleasePackage.ps1') `
        -PackageDirectory 'artifacts/packages' -Version $validationVersion -Commit $commit
}
finally
{
    Pop-Location
}
