[CmdletBinding()]
param(
    [ValidateSet('All', 'Restore', 'DotnetFormat', 'Build', 'RepositoryFormat', 'GateTests',
        'BenchmarkCorrectness', 'Tests', 'Performance', 'Package')]
    [string]$Step = 'All',
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$')]
    [string]$Version = '0.0.0-validation.1',
    [ValidateSet('Enforce', 'ReportOnly')][string]$TimingPolicy = 'Enforce'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$steps = @('Restore', 'DotnetFormat', 'Build', 'RepositoryFormat', 'GateTests',
    'BenchmarkCorrectness', 'Tests', 'Performance', 'Package')
$environment = @{ DOTNET_CLI_TELEMETRY_OPTOUT = '1'; DOTNET_NOLOGO = '1'; DOTNET_CLI_UI_LANGUAGE = 'en-US'; VSLANG = '1033' }
$previousEnvironment = @{}
foreach ($name in $environment.Keys)
{
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    [Environment]::SetEnvironmentVariable($name, $environment[$name], 'Process')
}

function Invoke-NativeCommand([string]$FilePath, [string[]]$ArgumentList)
{
    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) { throw "'$FilePath' exited with code $LASTEXITCODE." }
}

Push-Location $repoRoot
try
{
    foreach ($currentStep in $(if ($Step -eq 'All') { $steps } else { @($Step) }))
    {
        Write-Host "Validation step: $currentStep (version $Version; timing $TimingPolicy)"
        switch ($currentStep)
        {
            'Restore'
            {
                Invoke-NativeCommand 'dotnet' @('restore', 'PNFmt.slnx', "-p:Version=$Version")
            }
            'DotnetFormat'
            {
                $output = @(& dotnet format PNFmt.slnx --verify-no-changes --no-restore --verbosity diagnostic 2>&1)
                $exitCode = $LASTEXITCODE
                $output | ForEach-Object { Write-Host $_ }
                if ($exitCode -ne 0 -or
                    ($output -match 'Found project reference without a matching metadata reference:'))
                {
                    throw 'dotnet format failed or reported a project reference it could not load.'
                }
            }
            'Build'
            {
                # Build the requested package version once. Tests and packing reuse these binaries.
                Invoke-NativeCommand 'dotnet' @('build', 'PNFmt.slnx', '--configuration', 'Release',
                    '--no-restore', "-p:Version=$Version")
            }
            'RepositoryFormat'
            {
                & (Join-Path $repoRoot 'scripts/Format-Repository.ps1') -Check -Configuration Release -NoBuild
            }
            'GateTests'
            {
                & (Join-Path $PSScriptRoot 'Test-CoverageReport.Tests.ps1')
                & (Join-Path $PSScriptRoot 'Test-PerformanceReport.Tests.ps1')
                & (Join-Path $PSScriptRoot 'Test-ReleasePackage.Tests.ps1')
                Invoke-NativeCommand 'node' @('--test', 'build/select-github-release-build.test.cjs',
                    '.forge/scripts/create-release-tag.test.cjs')
            }
            'BenchmarkCorrectness'
            {
                Invoke-NativeCommand 'dotnet' @('run', '--project', 'benchmarks/PNFmt.Benchmarks.csproj',
                    '--configuration', 'Release', '--no-build', '--no-restore', '--', '--quick')
            }
            'Tests'
            {
                & (Join-Path $PSScriptRoot 'Test-Coverage.ps1') -Configuration Release -NoBuild -NoRestore
            }
            'Performance'
            {
                & (Join-Path $PSScriptRoot 'Test-Performance.ps1') -TimingPolicy $TimingPolicy
            }
            'Package'
            {
                $commit = (& git rev-parse HEAD | Out-String).Trim()
                if ($LASTEXITCODE -ne 0 -or $commit -cnotmatch '^[0-9a-f]{40}$')
                { throw 'Could not read the validated source commit.' }
                Invoke-NativeCommand 'dotnet' @('pack', 'PNFmt.Cli/PNFmt.Cli.csproj', '--configuration', 'Release',
                    '--no-build', '--no-restore', '--output', 'artifacts/packages',
                    "-p:Version=$Version", "-p:RepositoryCommit=$commit")
                $package = & (Join-Path $PSScriptRoot 'Test-ReleasePackage.ps1') `
                    -PackageDirectory 'artifacts/packages' -Version $Version -Commit $commit
                & (Join-Path $PSScriptRoot 'Test-ToolPackage.ps1') -PackagePath $package -Version $Version
                Write-Output $package
            }
        }
    }
}
finally
{
    Pop-Location
    foreach ($name in $previousEnvironment.Keys)
    {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
}
