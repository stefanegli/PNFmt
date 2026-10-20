[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$mappings = [Collections.Generic.List[object]]::new()
$previousTemp = $env:TEMP
$previousTmp = $env:TMP
$previousLocation = Get-Location

function Mount-BuildPath([string]$Path)
{
    $target = (Resolve-Path -LiteralPath $Path).Path.TrimEnd('\')
    foreach ($letter in 'ZYXWVUTSRQPONMLKJIHGFED'.ToCharArray())
    {
        $drive = "$($letter):"
        if ([IO.Directory]::GetLogicalDrives() -contains "$drive\") { continue }
        & subst.exe $drive $target
        if ($LASTEXITCODE -ne 0) { throw "Could not map '$target' to $drive." }
        $mappings.Add([pscustomobject]@{ Drive = $drive; Target = $target })
        return "$drive\"
    }
    throw 'No unused drive letter is available for the build.'
}

try
{
    # libgit2's Windows path normalization requires readable ancestors. Alias only
    # the assigned checkout and temp folder, without changing their storage or ACLs.
    $mappedRoot = Mount-BuildPath $repoRoot
    $mappedTemp = Mount-BuildPath ([IO.Path]::GetTempPath())
    $env:TEMP = $mappedTemp
    $env:TMP = $mappedTemp
    Set-Location -LiteralPath $mappedRoot
    & powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $mappedRoot 'scripts/Test-ForgeBuild.ps1')
    if ($LASTEXITCODE -ne 0) { throw "Forge validation exited with code $LASTEXITCODE." }
}
finally
{
    Set-Location -LiteralPath $previousLocation.Path
    $env:TEMP = $previousTemp
    $env:TMP = $previousTmp
    $cleanupErrors = [Collections.Generic.List[string]]::new()
    for ($index = $mappings.Count - 1; $index -ge 0; $index--)
    {
        $mapping = $mappings[$index]
        $currentMappings = @(& subst.exe)
        if ($LASTEXITCODE -ne 0 -or
            $currentMappings -notcontains "$($mapping.Drive)\: => $($mapping.Target)")
        {
            $cleanupErrors.Add("Build drive $($mapping.Drive) changed; refusing to remove it.")
            continue
        }
        & subst.exe $mapping.Drive /D
        if ($LASTEXITCODE -ne 0) { $cleanupErrors.Add("Could not remove build drive $($mapping.Drive).") }
    }
    if ($cleanupErrors.Count -gt 0) { throw ($cleanupErrors -join ' ') }
}
