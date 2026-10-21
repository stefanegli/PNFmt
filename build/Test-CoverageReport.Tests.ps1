# Regression checks for the build gate; no extra test framework is required.
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$gateScript = Join-Path $PSScriptRoot "Test-CoverageReport.ps1"
$fixtureDirectory = Join-Path $PSScriptRoot "../artifacts/coverage-gate-tests/$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force -Path $fixtureDirectory | Out-Null
$reportPath = Join-Path $fixtureDirectory "Summary.json"
$thresholdsPath = Join-Path $fixtureDirectory "thresholds.json"
$savedSummaryPath = $env:GITHUB_STEP_SUMMARY
$env:GITHUB_STEP_SUMMARY = $null
$casesPassed = 0

function Write-Fixture
{
    param([scriptblock]$Change = {})

    $fixture = @{
        Report = @{
            coverage = @{
                assemblies = @(
                    @{ name = "PNFmt.Core"; coveredlines = 100; coverablelines = 100; coveredbranches = 100; totalbranches = 100 },
                    @{ name = "pnfmt"; coveredlines = 80; coverablelines = 100; coveredbranches = 70; totalbranches = 100 }
                )
            }
        }
        Thresholds = @{
            "PNFmt.Core" = @{ line = 80; branch = 70 }
            "pnfmt" = @{ line = 80; branch = 70 }
        }
    }
    & $Change $fixture
    $fixture.Report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPath -Encoding utf8
    $fixture.Thresholds | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $thresholdsPath -Encoding utf8
}

function Assert-Gate
{
    param([string]$Name, [bool]$ShouldPass)

    $passed = $true
    try
    {
        & $gateScript -ReportPath $reportPath -ThresholdsPath $thresholdsPath 6>$null
    }
    catch
    {
        $passed = $false
    }
    if ($passed -ne $ShouldPass)
    {
        throw "Coverage gate regression: '$Name' expected pass=$ShouldPass, got pass=$passed."
    }
    $script:casesPassed++
    Write-Host "PASS: $Name"
}

try
{
    Write-Fixture
    Assert-Gate "Threshold equality passes" $true

    Write-Fixture { param($f) $f.Report.coverage.assemblies[1].coveredlines = 79 }
    Assert-Gate "Low CLI line coverage fails even with fully covered Core" $false

    Write-Fixture { param($f) $f.Report.coverage.assemblies[0].coveredbranches = 69 }
    Assert-Gate "Low Core branch coverage fails independently" $false

    Write-Fixture {
        param($f)
        $f.Report.coverage.assemblies[1].coveredlines = 799999
        $f.Report.coverage.assemblies[1].coverablelines = 1000000
    }
    Assert-Gate "Percentages that round up to the minimum still fail" $false

    Write-Fixture { param($f) $f.Report.coverage.assemblies = @($f.Report.coverage.assemblies[0]) }
    Assert-Gate "Missing assembly fails" $false

    Write-Fixture { param($f) $f.Report.coverage.assemblies += $f.Report.coverage.assemblies[0] }
    Assert-Gate "Duplicate assembly fails" $false

    Write-Fixture { param($f) $f.Report.coverage.assemblies = @() }
    Assert-Gate "Empty coverage fails" $false

    Write-Fixture {
        param($f)
        $f.Report.coverage.assemblies[0].coveredbranches = 0
        $f.Report.coverage.assemblies[0].totalbranches = 0
    }
    Assert-Gate "No measured branches fails" $false

    Write-Fixture { param($f) $f.Report.coverage.assemblies[0].coveredlines = 101 }
    Assert-Gate "Impossible coverage counts fail" $false

    Write-Fixture { param($f) $f.Report.coverage.assemblies[0].coveredlines = 99.5 }
    Assert-Gate "Fractional coverage counts fail" $false

    Write-Fixture { param($f) $f.Report.coverage.assemblies[0].Remove("coveredlines") }
    Assert-Gate "Missing metric fails" $false

    Write-Fixture { param($f) $f.Thresholds.Remove("pnfmt") }
    Assert-Gate "Missing assembly threshold fails" $false

    Write-Fixture { param($f) $f.Thresholds.pnfmt.line = -1 }
    Assert-Gate "Negative threshold fails" $false

    Write-Fixture { param($f) $f.Thresholds.pnfmt.branch = 101 }
    Assert-Gate "Threshold above 100 fails" $false

    Write-Fixture { param($f) $f.Thresholds.pnfmt.line = $null }
    Assert-Gate "Null threshold fails" $false

    Write-Fixture { param($f) $f.Thresholds.pnfmt.line = "NaN" }
    Assert-Gate "Nonnumeric threshold fails" $false

    "invalid JSON" | Set-Content -LiteralPath $reportPath
    Assert-Gate "Malformed report fails" $false

    $reportPath = Join-Path $fixtureDirectory "missing.json"
    Assert-Gate "Missing report fails" $false

    Write-Host "All $casesPassed coverage gate regression checks passed."
}
finally
{
    $env:GITHUB_STEP_SUMMARY = $savedSummaryPath
}
