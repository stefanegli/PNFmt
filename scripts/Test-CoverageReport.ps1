[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ReportPath,
    [string]$ThresholdsPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ThresholdsPath))
{
    $ThresholdsPath = Join-Path $PSScriptRoot "../coverage-thresholds.json"
}

function Get-Number
{
    param($Value, [string]$Description)

    if ($null -eq $Value -or
        -not ($Value -is [int] -or $Value -is [long] -or $Value -is [double] -or $Value -is [decimal]) -or
        [double]::IsNaN([double]$Value) -or [double]::IsInfinity([double]$Value))
    {
        throw "$Description must be a finite JSON number."
    }
    return [decimal]$Value
}

$report = Get-Content -LiteralPath $ReportPath -Raw | ConvertFrom-Json
$thresholds = Get-Content -LiteralPath $ThresholdsPath -Raw | ConvertFrom-Json
$expectedAssemblies = @("PNFmt.Core", "pnfmt")
$failures = @()
$rows = @()

# Require both production assemblies even if someone accidentally removes a filter
# or threshold entry. A missing assembly must never look like perfect coverage.
foreach ($assemblyName in $expectedAssemblies)
{
    $assemblyReports = @($report.coverage.assemblies | Where-Object { $_.name -ceq $assemblyName })
    if ($assemblyReports.Count -ne 1)
    {
        throw "Expected exactly one coverage entry for '$assemblyName'; found $($assemblyReports.Count)."
    }

    $assembly = $assemblyReports[0]
    $minimums = $thresholds.$assemblyName
    foreach ($metric in @(
        @{ Name = "line"; Covered = "coveredlines"; Total = "coverablelines" },
        @{ Name = "branch"; Covered = "coveredbranches"; Total = "totalbranches" }
    ))
    {
        $metricName = $metric.Name
        $minimum = Get-Number $minimums.$metricName "$assemblyName $metricName threshold"
        if ($minimum -lt 0 -or $minimum -gt 100)
        {
            throw "$assemblyName $metricName threshold must be between 0 and 100."
        }

        $covered = Get-Number $assembly.($metric.Covered) "$assemblyName covered $metricName count"
        $total = Get-Number $assembly.($metric.Total) "$assemblyName total $metricName count"
        if ($total -le 0 -or $covered -lt 0 -or $covered -gt $total -or
            $total -ne [decimal]::Truncate($total) -or $covered -ne [decimal]::Truncate($covered))
        {
            throw "$assemblyName has invalid or empty $metricName coverage counts ($covered/$total)."
        }

        # Compare counts directly; rounded report percentages must not pass a gate.
        $passed = (100 * $covered) -ge ($minimum * $total)
        $percentage = (100 * $covered / $total).ToString("F2", [Globalization.CultureInfo]::InvariantCulture)
        $minimumText = $minimum.ToString([Globalization.CultureInfo]::InvariantCulture)
        $status = if ($passed) { "Pass" } else { "FAIL" }
        $rows += "| $assemblyName | $metricName | $covered / $total | $percentage% | $minimumText% | $status |"
        if (-not $passed)
        {
            $failures += "$assemblyName $metricName coverage is $percentage%, below $minimumText%."
        }
    }
}

$summary = @(
    "### Coverage thresholds",
    "",
    "| Assembly | Metric | Covered / total | Coverage | Minimum | Result |",
    "| --- | --- | ---: | ---: | ---: | --- |"
) + $rows
$summary | ForEach-Object { Write-Host $_ }
$summary | Set-Content -LiteralPath (Join-Path (Split-Path -Parent (Resolve-Path -LiteralPath $ReportPath).Path) "CoverageGate.md") -Encoding utf8
if ($env:GITHUB_STEP_SUMMARY)
{
    $summary | Out-File -LiteralPath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
}

if ($failures.Count -gt 0)
{
    throw ($failures -join [Environment]::NewLine)
}
