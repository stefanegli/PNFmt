[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$PackageDirectory,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$')]
    [string]$Version,
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-f]{40}$')][string]$Commit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$packages = @(Get-ChildItem -LiteralPath $PackageDirectory -Filter '*.nupkg' -File -Recurse)
if ($packages.Count -ne 1)
{
    throw "Expected exactly one release package; found $($packages.Count)."
}

$archive = [IO.Compression.ZipFile]::OpenRead($packages[0].FullName)
try
{
    $specs = @($archive.Entries | Where-Object { $_.FullName -match '^[^/]+\.nuspec$' })
    if ($specs.Count -ne 1) { throw 'Expected exactly one package manifest.' }
    $stream = $specs[0].Open()
    $settings = New-Object Xml.XmlReaderSettings
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $reader = [Xml.XmlReader]::Create($stream, $settings)
    try
    {
        $manifest = New-Object Xml.XmlDocument
        $manifest.Load($reader)
        $metadata = $manifest.package.metadata
        if ($metadata.id -cne 'PetchNaka.PNFmt.Cli' -or $metadata.version -cne $Version)
        {
            throw "Package identity must be PetchNaka.PNFmt.Cli version $Version."
        }
        if ($metadata.repository.commit -cne $Commit)
        {
            throw "Package source commit does not match $Commit."
        }
    }
    finally
    {
        $reader.Dispose()
        $stream.Dispose()
    }
}
finally
{
    $archive.Dispose()
}

Write-Output $packages[0].FullName
