[CmdletBinding()]
param(
    [switch]$Check,
    [string]$Configuration = "Release",
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

Push-Location $repoRoot
try
{
    # Some fixtures deliberately opt into formatting in a root=true EditorConfig.
    # Exclude them before invoking PNFmt, independently of inherited settings.
    $gitFiles = @(git -c core.quotepath=false ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0)
    {
        throw "Could not enumerate repository files."
    }

    $files = @($gitFiles | Where-Object {
        $_ -notmatch '^Snapshots/|(^|/)(_files|_editor|bin|obj|artifacts|node_modules|\.dotnet)/' -and
        $_ -match '\.(cs|csproj|props|targets|xml|xaml|resx|rsp|slnx|ini|config|runsettings)$|(^|/)\.editorconfig$' -and
        (Test-Path -LiteralPath $_ -PathType Leaf)
    } | Sort-Object -Unique)
    if ($files.Count -eq 0)
    {
        throw "No repository source or configuration files were selected."
    }

    if (-not $NoBuild)
    {
        dotnet build PNFmt.Cli/PNFmt.Cli.csproj --configuration $Configuration --nologo
        if ($LASTEXITCODE -ne 0)
        {
            throw "Building the repository formatter failed with exit code $LASTEXITCODE."
        }
    }

    $formatterPath = Join-Path $repoRoot "PNFmt.Cli/bin/$Configuration/net10.0/pnfmt.dll"
    if (-not (Test-Path -LiteralPath $formatterPath -PathType Leaf))
    {
        throw "Formatter not found: $formatterPath. Run without -NoBuild first."
    }

    Write-Host "Selected $($files.Count) source and configuration files; fixture data and expected snapshots are excluded."
    $formatterArguments = @($formatterPath, "--all", "--verbose")
    if ($Check) { $formatterArguments += "--check" }

    # Keep each invocation below Windows' command-line length limit.
    $failed = $false
    for ($offset = 0; $offset -lt $files.Count; $offset += 25)
    {
        $last = [Math]::Min($offset + 24, $files.Count - 1)
        $batch = @($files[$offset..$last])
        & dotnet @formatterArguments -- @batch
        if ($LASTEXITCODE -ne 0) { $failed = $true }
    }
    if ($failed)
    {
        throw "Repository formatting failed or -Check found files that need formatting."
    }
}
finally
{
    Pop-Location
}
