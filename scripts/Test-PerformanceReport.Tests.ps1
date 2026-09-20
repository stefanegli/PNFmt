# Exercise gate failures independently of timing noise or the production formatter.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$gateScript = Join-Path $PSScriptRoot 'Test-PerformanceReport.ps1'
$directory = Join-Path $PSScriptRoot "../artifacts/performance-gate-tests/$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$baselinePath = Join-Path $directory 'baseline.json'
$savedSummary = $env:GITHUB_STEP_SUMMARY
$env:GITHUB_STEP_SUMMARY = $null
$passedCount = 0

function Write-Fixture([scriptblock]$Change = {})
{
    $fixture = @{
        Policy = @{
            SuiteVersion = 1
            Revision = 'd3ac668e7eefa4d2480c874c0164a4c710658961'
            Reason = 'Regression test fixture'
            Tolerances = @{ TimePercent = 20; WriteTimePercent = 50; AllocationPercent = 10; TimeFloorMilliseconds = 0.1; AllocationFloorBytes = 4096 }
        }
        Reports = @{}
    }
    $counts = @{ xml = 6; xaml = 6; csproj = 6; csharp = 12; repository = 29 }
    foreach ($family in $counts.Keys)
    {
        foreach ($revision in @('baseline', 'current'))
        {
            foreach ($round in 1..3)
            {
                $sampleCount = if ($family -eq 'repository') { 7 } else { 9 }
                $fixture.Reports["$revision-$family-$round"] = @{
                    SuiteVersion = 1; Family = $family; TieredCompilation = '0'
                    Runtime = '.NET 10 test'; OS = 'test OS'; Architecture = 'X64'; ProcessorCount = 4
                    Cases = @(0..($counts[$family] - 1) | ForEach-Object {
                        @{
                            Id = "$family/case$_"; Write = ($family -eq 'repository'); Iterations = 10
                            Milliseconds = @(1..$sampleCount | ForEach-Object { 10.0 })
                            AllocatedBytes = @(1..$sampleCount | ForEach-Object { 100000.0 })
                            OutputHash = if ($family -eq 'repository') { $null } else { 'A' * 64 }
                        }
                    })
                }
            }
        }
    }
    & $Change $fixture
    $fixture.Policy | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $baselinePath -Encoding utf8
    foreach ($name in $fixture.Reports.Keys)
    {
        $fixture.Reports[$name] | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $directory "$name.json") -Encoding utf8
    }
}

function Assert-Gate([string]$Name, [bool]$ShouldPass)
{
    $passed = $true
    try { & $gateScript -RunDirectory $directory -BaselinePath $baselinePath 6>$null }
    catch { $passed = $false }
    if ($passed -ne $ShouldPass) { throw "Performance gate test '$Name': expected pass=$ShouldPass; got $passed." }
    $script:passedCount++
    Write-Host "PASS: $Name"
}

try
{
    Write-Fixture
    Assert-Gate 'Identical measurements pass' $true
    Write-Fixture { param($f) $f.Reports['current-xml-1'].Cases[0].Milliseconds = @(1..9 | ForEach-Object { 1000.0 }) }
    Assert-Gate 'One noisy process does not outweigh two stable runs' $true
    Write-Fixture { param($f) foreach ($r in 1..2) { $f.Reports["current-xml-$r"].Cases[0].Milliseconds = @(1..9 | ForEach-Object { 12.001 }) } }
    Assert-Gate 'A repeatable regression fails despite one fast run' $false
    Write-Fixture { param($f) foreach ($r in 1..3) { $f.Reports["current-xml-$r"].Cases[0].Milliseconds = @(1..9 | ForEach-Object { 12.0 }) } }
    Assert-Gate 'Time equality at the limit passes' $true
    Write-Fixture { param($f) foreach ($r in 1..3) { $f.Reports["current-xml-$r"].Cases[0].Milliseconds = @(1..9 | ForEach-Object { 12.001 }) } }
    Assert-Gate 'A single slow case fails without averaging it away' $false
    if (-not (Select-String -LiteralPath (Join-Path $directory 'PerformanceGate.md') -Pattern '\| FAIL \|')) { throw 'Failure report was not retained.' }
    Write-Fixture { param($f) foreach ($r in 1..3) { $f.Reports["current-xml-$r"].Cases[0].AllocatedBytes = @(1..9 | ForEach-Object { 110000.0 }) } }
    Assert-Gate 'Allocation equality at the limit passes' $true
    Write-Fixture { param($f) foreach ($r in 1..3) { $f.Reports["current-xml-$r"].Cases[0].AllocatedBytes = @(1..9 | ForEach-Object { 110001.0 }) } }
    Assert-Gate 'Allocation regression fails even when time passes' $false
    Write-Fixture { param($f) foreach ($r in 1..3) { $f.Reports["current-repository-$r"].Cases[0].Milliseconds = @(1..7 | ForEach-Object { 15.0 }) } }
    Assert-Gate 'Writes use the documented larger time allowance' $true
    Write-Fixture { param($f) foreach ($r in 1..3) { $f.Reports["current-repository-$r"].Cases[0].Milliseconds = @(1..7 | ForEach-Object { 15.001 }) } }
    Assert-Gate 'Write regression beyond its limit fails' $false
    Write-Fixture { param($f)
        foreach ($r in 1..3) {
            $f.Reports["baseline-xml-$r"].Cases[0].Milliseconds = @(1..9 | ForEach-Object { 0.01 })
            $f.Reports["current-xml-$r"].Cases[0].Milliseconds = @(1..9 | ForEach-Object { 0.1 })
            $f.Reports["baseline-xml-$r"].Cases[0].AllocatedBytes = @(1..9 | ForEach-Object { 100.0 })
            $f.Reports["current-xml-$r"].Cases[0].AllocatedBytes = @(1..9 | ForEach-Object { 4196.0 })
        }
    }
    Assert-Gate 'Absolute floors avoid failing negligible differences' $true
    Write-Fixture { param($f) $f.Reports['current-xml-1'].Cases = @($f.Reports['current-xml-1'].Cases[0]) }
    Assert-Gate 'Missing cases fail' $false
    Write-Fixture { param($f) $f.Reports['current-xml-1'].Cases[1].Id = 'xml/case0' }
    Assert-Gate 'Duplicate cases fail' $false
    Write-Fixture { param($f) $f.Reports['current-xml-1'].Cases[0].Id = 'xml/replaced' }
    Assert-Gate 'Different case sets fail' $false
    Write-Fixture { param($f) $f.Reports['current-xml-1'].Cases[0].Milliseconds = @(10.0) }
    Assert-Gate 'Missing samples fail' $false
    Write-Fixture { param($f) $f.Reports['current-xml-1'].Cases[0].AllocatedBytes[0] = 0 }
    Assert-Gate 'Invalid sample values fail' $false
    Write-Fixture { param($f) $f.Policy.Tolerances.TimePercent = -1 }
    Assert-Gate 'Invalid tolerance fails' $false
    Write-Fixture { param($f) $f.Policy.Reason = '' }
    Assert-Gate 'A baseline change needs a reason' $false
    Write-Fixture { param($f) $f.Policy.Revision = 'HEAD' }
    Assert-Gate 'A moving baseline reference fails' $false
    Write-Fixture { param($f) $f.Reports['current-xml-1'].Runtime = 'another runtime' }
    Assert-Gate 'Different environments fail' $false
    Write-Fixture { param($f) $f.Reports['current-xml-1'].SuiteVersion = 2 }
    Assert-Gate 'Incompatible suite versions fail' $false
    Write-Fixture { param($f) $f.Reports['current-xml-1'].TieredCompilation = '1' }
    Assert-Gate 'Wrong timing configuration fails' $false
    Write-Fixture { param($f) $f.Reports['current-xml-1'].Cases[0].OutputHash = 'B' * 64 }
    Assert-Gate 'Non-repeatable outputs fail' $false
    Write-Fixture { param($f) foreach ($r in 1..3) { $f.Reports["current-xml-$r"].Cases[0].OutputHash = 'B' * 64 } }
    Assert-Gate 'Intentional output differences are reported' $true
    if (-not (Select-String -LiteralPath (Join-Path $directory 'PerformanceGate.md') -SimpleMatch '**changed**')) { throw 'Changed output was not visible.' }
    Write-Fixture
    Remove-Item -LiteralPath (Join-Path $directory 'current-xml-2.json')
    Assert-Gate 'A missing round fails' $false
    Write-Host "$passedCount performance gate regression checks passed."
}
finally
{
    $env:GITHUB_STEP_SUMMARY = $savedSummary
}
