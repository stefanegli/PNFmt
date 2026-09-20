[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$runDirectory = Join-Path $repoRoot "artifacts/performance/$([Guid]::NewGuid().ToString('N'))"
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$buildRoot = [IO.Path]::GetFullPath((Join-Path $temporaryRoot ("PNFmt-perf-" + [Guid]::NewGuid().ToString('N'))))
$savedTiering = $env:DOTNET_TieredCompilation
New-Item -ItemType Directory -Force -Path $runDirectory, $buildRoot | Out-Null
$baselinePath = Join-Path $runDirectory 'baseline.json'
Copy-Item -LiteralPath (Join-Path $repoRoot 'benchmarks/performance-baseline.json') -Destination $baselinePath
$policy = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json

function Invoke-Dotnet([string[]]$Arguments, [string]$LogPath)
{
    & dotnet @Arguments > $LogPath 2>&1
    if ($LASTEXITCODE -ne 0)
    {
        Get-Content -LiteralPath $LogPath -Tail 30 | ForEach-Object { Write-Host $_ }
        throw "Performance command failed with exit code $LASTEXITCODE. Log: $LogPath"
    }
}

Push-Location $repoRoot
try
{
    if ($policy.Revision -cnotmatch '^[0-9a-f]{40}$' -or $policy.SuiteVersion -ne 1 -or
        [string]::IsNullOrWhiteSpace($policy.Reason))
    {
        throw 'The performance baseline must pin a full commit ID and describe why it was chosen.'
    }
    & git cat-file -e "$($policy.Revision)^{commit}"
    if ($LASTEXITCODE -ne 0)
    {
        throw "Performance baseline $($policy.Revision) is unavailable locally. Fetch its history (CI uses fetch-depth: 0) and rerun."
    }
    $archive = Join-Path $buildRoot 'baseline.zip'
    & git archive --format=zip "--output=$archive" $policy.Revision
    if ($LASTEXITCODE -ne 0) { throw 'Could not export the performance baseline.' }
    $baselineSource = Join-Path $buildRoot 'source'
    Expand-Archive -LiteralPath $archive -DestinationPath $baselineSource

    $currentRevision = (& git rev-parse HEAD | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not identify the current revision.' }
    $currentStatus = (& git status --porcelain | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not identify working-tree changes.' }
    $harnessFiles = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'benchmarks') -File |
        Where-Object { $_.Extension -in @('.cs', '.csproj') } | Sort-Object Name)
    @{
        StartedUtc = [DateTime]::UtcNow.ToString('o')
        BaselineRevision = $policy.Revision
        CurrentRevision = $currentRevision
        CurrentDirty = -not [string]::IsNullOrWhiteSpace($currentStatus)
        HarnessFiles = @($harnessFiles | ForEach-Object { @{ Name = $_.Name; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runDirectory 'run.json') -Encoding utf8
    Copy-Item -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Destination $buildRoot

    # Build the SAME current harness twice. Only the referenced formatter/CLI
    # source revision changes; each copy has independent intermediate output.
    foreach ($revision in @('baseline', 'current'))
    {
        $harness = Join-Path $buildRoot $revision
        New-Item -ItemType Directory -Path $harness | Out-Null
        foreach ($file in $harnessFiles) { Copy-Item -LiteralPath $file.FullName -Destination $harness }
        $source = if ($revision -eq 'baseline') { $baselineSource } else { $repoRoot }
        Write-Host "Building $revision performance harness..."
        Invoke-Dotnet @('build', (Join-Path $harness 'PNFmt.Benchmarks.csproj'), '--configuration', 'Release',
            '--output', (Join-Path $buildRoot "bin-$revision"), "-p:FormatterSourceRoot=$source", '--nologo') `
            (Join-Path $runDirectory "build-$revision.log")
    }

    # Timing does not overlap builds or another benchmark. Separate processes
    # isolate each family; swapping revision order reduces drift bias.
    $env:DOTNET_TieredCompilation = '0'
    foreach ($round in 1..3)
    {
        foreach ($family in @('xml', 'xaml', 'csproj', 'csharp', 'repository'))
        {
            $revisions = if ($round % 2 -eq 1) { @('baseline', 'current') } else { @('current', 'baseline') }
            foreach ($revision in $revisions)
            {
                $name = "$revision-$family-$round"
                Write-Host "Measuring $name..."
                Invoke-Dotnet @((Join-Path $buildRoot "bin-$revision/PNFmt.Benchmarks.dll"), '--performance', $family,
                    (Join-Path $runDirectory "$name.json")) (Join-Path $runDirectory "$name.log")
            }
        }
    }
    & (Join-Path $PSScriptRoot 'Test-PerformanceReport.ps1') -RunDirectory $runDirectory -BaselinePath $baselinePath
}
finally
{
    $env:DOTNET_TieredCompilation = $savedTiering
    Pop-Location
    Write-Host "Performance artifacts: $runDirectory"
    if (Test-Path -LiteralPath $buildRoot)
    {
        if ([IO.Path]::GetDirectoryName($buildRoot) -ne $temporaryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) -or
            -not [IO.Path]::GetFileName($buildRoot).StartsWith('PNFmt-perf-', [StringComparison]::Ordinal))
        {
            throw "Refusing to clean up outside the performance temporary root: $buildRoot"
        }
        Remove-Item -LiteralPath $buildRoot -Recurse -Force
    }
}
