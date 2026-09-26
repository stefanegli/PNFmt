[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RunDirectory,
    [Parameter(Mandatory = $true)][string]$BaselinePath,
    [ValidateSet('Enforce', 'ReportOnly')][string]$TimingPolicy = 'Enforce'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$culture = [Globalization.CultureInfo]::InvariantCulture
$policy = Get-Content -LiteralPath $BaselinePath -Raw | ConvertFrom-Json
$counts = @{ xml = 6; xaml = 6; csproj = 6; csharp = 12; resx = 8; repository = 29 }

function Assert-Number($Value, [bool]$AllowZero = $false)
{
    if ($null -eq $Value -or $Value -isnot [ValueType] -or $Value -is [bool] -or
        [double]::IsNaN([double]$Value) -or [double]::IsInfinity([double]$Value) -or
        [double]$Value -lt 0 -or (-not $AllowZero -and [double]$Value -eq 0))
    {
        throw "Performance reports and tolerances must contain finite positive numbers (zero only for tolerances)."
    }
}

function Get-Median($Values)
{
    $sorted = @($Values | Sort-Object)
    $middle = [int][Math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2) { return [double]$sorted[$middle] }
    return ([double]$sorted[$middle - 1] + [double]$sorted[$middle]) / 2
}

function Format-Number([double]$Value, [string]$Pattern = "F3")
{
    return $Value.ToString($Pattern, $culture)
}

if ($policy.SuiteVersion -ne 2 -or $policy.Revision -cnotmatch '^[0-9a-f]{40}$' -or
    [string]::IsNullOrWhiteSpace($policy.Reason))
{
    throw "The performance baseline requires suite version 2, a full commit ID, and a reason."
}
foreach ($property in @('TimePercent', 'WriteTimePercent', 'AllocationPercent', 'TimeFloorMilliseconds', 'AllocationFloorBytes'))
{
    Assert-Number $policy.Tolerances.$property $true
}

$measurements = @{}
$environmentIdentity = $null
foreach ($family in @('xml', 'xaml', 'csproj', 'csharp', 'resx', 'repository'))
{
    $expectedIds = $null
    foreach ($revision in @('baseline', 'current'))
    {
        foreach ($round in 1..3)
        {
            $path = Join-Path $RunDirectory "$revision-$family-$round.json"
            $report = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
            if ($report.SuiteVersion -ne $policy.SuiteVersion -or $report.Family -cne $family -or
                $report.TieredCompilation -cne '0' -or @($report.Cases).Count -ne $counts[$family])
            {
                throw "Incomplete or incompatible performance report: $path"
            }
            foreach ($property in @('Runtime', 'OS', 'Architecture'))
            {
                if ([string]::IsNullOrWhiteSpace($report.$property)) { throw "Missing environment metadata: $path" }
            }
            Assert-Number $report.ProcessorCount
            $identity = @($report.Runtime, $report.OS, $report.Architecture, $report.ProcessorCount) -join '|'
            if ($null -eq $environmentIdentity) { $environmentIdentity = $identity }
            if ($identity -cne $environmentIdentity) { throw "Performance reports must use the same runtime and machine: $path" }
            $ids = @{}
            foreach ($case in $report.Cases)
            {
                if ([string]::IsNullOrWhiteSpace($case.Id) -or -not $case.Id.StartsWith("$family/", [StringComparison]::Ordinal) -or
                    $ids.ContainsKey($case.Id) -or $case.Write -isnot [bool])
                {
                    throw "Invalid or duplicate performance case: $path"
                }
                $ids[$case.Id] = $true
                Assert-Number $case.Iterations
                if ($case.Iterations -ne [Math]::Floor($case.Iterations)) { throw "Invalid iteration count: $path" }
                $sampleCount = if ($family -eq 'repository') { 7 } else { 9 }
                if (@($case.Milliseconds).Count -ne $sampleCount -or @($case.AllocatedBytes).Count -ne $sampleCount)
                {
                    throw "Missing performance samples: $path"
                }
                foreach ($sample in @($case.Milliseconds) + @($case.AllocatedBytes)) { Assert-Number $sample }
                if ($family -ne 'repository' -and $case.OutputHash -cnotmatch '^[0-9A-F]{64}$')
                {
                    throw "Missing output fingerprint: $path"
                }
                $key = "$revision/$($case.Id)"
                if (-not $measurements.ContainsKey($key)) { $measurements[$key] = @() }
                $measurements[$key] += $case
            }
            $caseIds = @($ids.Keys | Sort-Object) -join "`n"
            if ($null -eq $expectedIds) { $expectedIds = $caseIds }
            if ($caseIds -cne $expectedIds) { throw "Performance cases changed between reports: $path" }
        }
    }
}

$lines = [Collections.Generic.List[string]]::new()
$lines.Add('# PNFmt performance gate')
$lines.Add('')
$lines.Add("Baseline: ``$($policy.Revision)``. $($policy.Reason)")
$lines.Add("Environment: $environmentIdentity. Three process runs; medians of 7 repository or 9 formatter samples, then the median of all three runs.")
$lines.Add("Timing policy: **$TimingPolicy**. Allocation limits, report validity, and output stability are always enforced.")
$lines.Add("Limits: time +$($policy.Tolerances.TimePercent)%, writes +$($policy.Tolerances.WriteTimePercent)%, allocations +$($policy.Tolerances.AllocationPercent)%. Noise floors: $($policy.Tolerances.TimeFloorMilliseconds) ms and $($policy.Tolerances.AllocationFloorBytes) bytes; the larger allowance applies.")
$lines.Add('')
$lines.Add('| Case | Baseline ms | Current ms | Time change | Baseline KiB | Current KiB | Allocation change | Output | Result |')
$lines.Add('| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |')
$failures = [Collections.Generic.List[string]]::new()
$timingWarnings = [Collections.Generic.List[string]]::new()
foreach ($key in @($measurements.Keys | Where-Object { $_.StartsWith('baseline/') } | Sort-Object))
{
    $id = $key.Substring(9)
    $before = $measurements[$key]
    $after = $measurements["current/$id"]
    if ($before.Count -ne 3 -or $after.Count -ne 3 -or
        @($before | ForEach-Object { [string]$_.OutputHash } | Select-Object -Unique).Count -ne 1 -or
        @($after | ForEach-Object { [string]$_.OutputHash } | Select-Object -Unique).Count -ne 1 -or
        @($before.Write + $after.Write | Select-Object -Unique).Count -ne 1)
    {
        throw "Non-repeatable output or inconsistent case metadata: $id"
    }
    $beforeTime = Get-Median @($before | ForEach-Object { Get-Median $_.Milliseconds })
    $afterTime = Get-Median @($after | ForEach-Object { Get-Median $_.Milliseconds })
    $beforeBytes = Get-Median @($before | ForEach-Object { Get-Median $_.AllocatedBytes })
    $afterBytes = Get-Median @($after | ForEach-Object { Get-Median $_.AllocatedBytes })
    $timePercent = if ($before[0].Write) { $policy.Tolerances.WriteTimePercent } else { $policy.Tolerances.TimePercent }
    $timeLimit = $beforeTime + [Math]::Max($beforeTime * $timePercent / 100, $policy.Tolerances.TimeFloorMilliseconds)
    $byteLimit = $beforeBytes + [Math]::Max($beforeBytes * $policy.Tolerances.AllocationPercent / 100, $policy.Tolerances.AllocationFloorBytes)
    $timeExceeded = $afterTime -gt $timeLimit
    $allocationExceeded = $afterBytes -gt $byteLimit
    $failed = $allocationExceeded -or ($timeExceeded -and $TimingPolicy -eq 'Enforce')
    if ($timeExceeded -and $TimingPolicy -eq 'ReportOnly') { $timingWarnings.Add($id) }
    if ($failed) { $failures.Add($id) }
    $outcome = if ($failed) { 'FAIL' } elseif ($timeExceeded) { 'TIMING WARNING' } else { 'PASS' }
    $output = if ($null -eq $before[0].OutputHash) { 'validated' } elseif ($before[0].OutputHash -ceq $after[0].OutputHash) { 'same' } else { '**changed**' }
    $lines.Add("| $id | $(Format-Number $beforeTime) | $(Format-Number $afterTime) | $(Format-Number (100 * ($afterTime / $beforeTime - 1)) 'F1')% | $(Format-Number ($beforeBytes / 1024) 'F1') | $(Format-Number ($afterBytes / 1024) 'F1') | $(Format-Number (100 * ($afterBytes / $beforeBytes - 1)) 'F1')% | $output | $outcome |")
}
$lines.Add('')
$lines.Add("$($measurements.Count / 2) cases compared; $($failures.Count) failed enforced limits; $($timingWarnings.Count) report-only timing warnings. Raw samples and run metadata are retained alongside this report.")
$reportPath = Join-Path $RunDirectory 'PerformanceGate.md'
$lines | Set-Content -LiteralPath $reportPath -Encoding utf8
if ($env:GITHUB_STEP_SUMMARY) { $lines | Out-File -LiteralPath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8 }
Write-Host "Performance report: $reportPath"
if ($timingWarnings.Count -gt 0)
{
    Write-Warning "Timing limits exceeded (report-only): $($timingWarnings -join ', '). Review timing on the controlled release machine."
}
if ($failures.Count -gt 0)
{
    throw "Performance regression in: $($failures -join ', '). Investigate the report; intentional costs require a reviewed baseline update with a reason."
}
Write-Host "Performance gate passed: $($measurements.Count / 2) cases; timing policy $TimingPolicy; $($timingWarnings.Count) timing warnings."
