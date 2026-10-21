[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$SolutionPath = "PNFmt.slnx",
    [switch]$NoBuild,
    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
# Use a fresh directory for each run so old reports can never satisfy the gate.
$runDirectory = Join-Path $repoRoot "artifacts/coverage/$([Guid]::NewGuid().ToString('N'))"
$resultsDirectory = Join-Path $runDirectory "results"
$reportDirectory = Join-Path $runDirectory "report"
New-Item -ItemType Directory -Force -Path $resultsDirectory | Out-Null

Push-Location $repoRoot
try
{
    dotnet tool restore --tool-manifest .config/dotnet-tools.json
    if ($LASTEXITCODE -ne 0)
    {
        throw "Coverage report tool restore failed with exit code $LASTEXITCODE."
    }

    $testArguments = @(
        "test", $SolutionPath,
        "--configuration", $Configuration,
        "--collect:XPlat Code Coverage",
        "--settings", (Join-Path $repoRoot "coverage.runsettings"),
        "--results-directory", $resultsDirectory,
        "--logger", "trx",
        "--nologo"
    )
    if ($NoBuild) { $testArguments += "--no-build" }
    if ($NoRestore) { $testArguments += "--no-restore" }

    & dotnet @testArguments
    $testExitCode = $LASTEXITCODE

    $coverageFiles = @(Get-ChildItem -LiteralPath $resultsDirectory -Filter coverage.cobertura.xml -Recurse -File)
    if ($coverageFiles.Count -eq 0)
    {
        throw "No coverage report was produced. Test exit code: $testExitCode. Results: $resultsDirectory"
    }

    # Generate reports even when tests failed, before enforcing coverage thresholds.
    dotnet tool run reportgenerator -- `
        "-reports:$($coverageFiles.FullName -join ';')" `
        "-targetdir:$reportDirectory" `
        "-reporttypes:Html;JsonSummary;MarkdownSummaryGithub" `
        "-historydir:$(Join-Path $repoRoot 'artifacts/coverage-history')" `
        "-title:PNFmt test coverage"
    if ($LASTEXITCODE -ne 0)
    {
        throw "Coverage report generation failed with exit code $LASTEXITCODE."
    }

    Write-Host "Coverage report: $(Join-Path $reportDirectory 'index.html')"
    if ($env:GITHUB_STEP_SUMMARY)
    {
        Get-Content -LiteralPath (Join-Path $reportDirectory "SummaryGithub.md") -Raw |
            Out-File -LiteralPath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
    }

    & (Join-Path $PSScriptRoot "Test-CoverageReport.ps1") `
        -ReportPath (Join-Path $reportDirectory "Summary.json") `
        -ThresholdsPath (Join-Path $repoRoot "coverage-thresholds.json")

    if ($testExitCode -ne 0)
    {
        throw "Tests failed with exit code $testExitCode. Coverage reports are available at $reportDirectory."
    }
}
finally
{
    Pop-Location
}
