# These packages contain only metadata; installed-tool behavior has its own check.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$testRoot = Join-Path $PSScriptRoot "../artifacts/release-package-tests/$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force -Path $testRoot | Out-Null
$commit = 'a' * 40
$version = '0.1.0-alpha.12'
$passed = 0

function Test-PackageCase([string]$Name, [string]$Metadata, [bool]$ShouldPass, [int]$Count = 1)
{
    $directory = Join-Path $testRoot $Name
    New-Item -ItemType Directory -Path $directory | Out-Null
    for ($i = 0; $i -lt $Count; $i++)
    {
        $archive = [IO.Compression.ZipFile]::Open((Join-Path $directory "package$i.nupkg"), [IO.Compression.ZipArchiveMode]::Create)
        try
        {
            $writer = New-Object IO.StreamWriter($archive.CreateEntry('package.nuspec').Open())
            try { $writer.Write($Metadata) }
            finally { $writer.Dispose() }
        }
        finally { $archive.Dispose() }
    }
    $accepted = $true
    try
    {
        $result = & (Join-Path $PSScriptRoot 'Test-ReleasePackage.ps1') -PackageDirectory $directory -Version $version -Commit $commit
    }
    catch { $accepted = $false }
    if ($accepted -ne $ShouldPass) { throw "Package test '$Name': expected accepted=$ShouldPass, got $accepted." }
    if ($accepted -and $result -cne (Join-Path $directory 'package0.nupkg'))
    {
        # Resolve paths before comparing because the test root deliberately contains '..'.
        if ($result -cne (Resolve-Path -LiteralPath (Join-Path $directory 'package0.nupkg')).Path)
        { throw 'Validation did not return the selected package path.' }
    }
    $script:passed++
    Write-Host "PASS: $Name"
}

$valid = "<package xmlns=`"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd`"><metadata><id>PetchNaka.PNFmt.Cli</id><version>$version</version><repository type=`"git`" commit=`"$commit`" /></metadata></package>"
Test-PackageCase 'matching-version-and-commit' $valid $true
Test-PackageCase 'wrong-version' ($valid.Replace($version, '0.1.0')) $false
Test-PackageCase 'wrong-commit' ($valid.Replace($commit, ('b' * 40))) $false
Test-PackageCase 'wrong-package' ($valid.Replace('PetchNaka.PNFmt.Cli', 'Other.Package')) $false
Test-PackageCase 'missing-commit' ($valid.Replace("commit=`"$commit`"", '')) $false
Test-PackageCase 'malformed-manifest' '<broken' $false
Test-PackageCase 'dtd-prohibited' ('<!DOCTYPE package [<!ENTITY version "' + $version + '">]>' + $valid.Replace($version, '&version;')) $false
Test-PackageCase 'missing-package' $valid $false 0
Test-PackageCase 'ambiguous-packages' $valid $false 2
Write-Host "$passed release package checks passed."
