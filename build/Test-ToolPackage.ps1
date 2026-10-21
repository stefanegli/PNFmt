[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$')]
    [string]$Version,
    [string]$PackageId = 'PetchNaka.PNFmt.Cli',
    [string]$ToolCommandName = 'pnfmt'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$resolvedPackagePath = (Resolve-Path -LiteralPath $PackagePath).Path

function Invoke-NativeCommand
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0)
    {
        throw "'$FilePath' exited with code $LASTEXITCODE."
    }
}

$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$validationRoot = [IO.Path]::GetFullPath((Join-Path $temporaryRoot (
    "PNFmt-tool-validation-" + [Guid]::NewGuid().ToString("N"))))
$toolPath = Join-Path $validationRoot "tools"
$originalNuGetPackages = $env:NUGET_PACKAGES

try
{
    New-Item -ItemType Directory -Force -Path $toolPath | Out-Null
    $env:NUGET_PACKAGES = Join-Path $validationRoot "packages"
    $nuGetConfigPath = Join-Path $validationRoot "NuGet.Config"
    # Use only the selected package, even if the caller's directory contains others.
    $feedPath = Join-Path $validationRoot 'feed'
    New-Item -ItemType Directory -Path $feedPath | Out-Null
    Copy-Item -LiteralPath $resolvedPackagePath -Destination $feedPath
    $escapedSource = [Security.SecurityElement]::Escape($feedPath)
    [IO.File]::WriteAllText($nuGetConfigPath,
        "<configuration><packageSources><clear/><add key=`"local`" value=`"$escapedSource`"/></packageSources></configuration>")

    Write-Host "Installing the package into an isolated tool path..."
    Invoke-NativeCommand "dotnet" @(
        "tool",
        "install",
        $packageId,
        "--tool-path", $toolPath,
        "--configfile", $nuGetConfigPath,
        "--version", $Version
    )

    $toolExecutableName = if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT)
    {
        "$toolCommandName.exe"
    }
    else
    {
        $toolCommandName
    }
    $toolExecutable = Join-Path $toolPath $toolExecutableName
    if (-not (Test-Path -LiteralPath $toolExecutable -PathType Leaf))
    {
        throw "Installed tool command was not found: '$toolExecutable'."
    }

    $toolVersionOutput = (& $toolExecutable --version | Out-String).Trim()
    if ($LASTEXITCODE -ne 0)
    {
        throw "The installed '$toolCommandName --version' command failed with code $LASTEXITCODE."
    }

    if (-not $toolVersionOutput.Equals("$toolCommandName $Version", [StringComparison]::Ordinal))
    {
        throw "Expected '$toolCommandName $Version' from installed tool, received '$toolVersionOutput'."
    }

    $fixturePath = Join-Path $validationRoot "fixtures"
    New-Item -ItemType Directory -Path $fixturePath | Out-Null
    $utf8 = New-Object Text.UTF8Encoding($false)
    $editorConfig = "root = true`n`n[*]`ncharset = utf-8`nend_of_line = lf`nindent_style = space`nindent_size = 2`ninsert_final_newline = true`n"
    $editorConfig += "`n[*.cs]`npnfmt_csharp_format = true`ncsharp_new_line_before_open_brace = all`n"
    $editorConfig += "`n[*.resx]`npnfmt_sort_entries = true`n"
    [IO.File]::WriteAllText((Join-Path $fixturePath ".editorconfig"), $editorConfig, $utf8)
    $sourcePath = Join-Path $fixturePath "Sample.cs"
    $resourcePath = Join-Path $fixturePath "Strings.resx"
    $sourceInput = "class C{`nvoid M(){`nif(true){`nM();`n}`n}`n}`n"
    $resourceInput = '<root><resheader name="resmimetype"><value>text/microsoft-resx</value></resheader><data name="b"><value>b</value></data><data name="a"><value>a</value></data></root>'
    [IO.File]::WriteAllText($sourcePath, $sourceInput, $utf8)
    [IO.File]::WriteAllText($resourcePath, $resourceInput, $utf8)
    $sourceBytes = [Convert]::ToBase64String([IO.File]::ReadAllBytes($sourcePath))
    $resourceBytes = [Convert]::ToBase64String([IO.File]::ReadAllBytes($resourcePath))

    Write-Host "Checking installed-tool preview, formatting, and idempotence..."
    & $toolExecutable --all --check $sourcePath $resourcePath
    if ($LASTEXITCODE -ne 1)
    {
        throw "The installed tool did not detect unformatted C# and RESX files (exit $LASTEXITCODE)."
    }

    if ($sourceBytes -cne [Convert]::ToBase64String([IO.File]::ReadAllBytes($sourcePath)) -or
        $resourceBytes -cne [Convert]::ToBase64String([IO.File]::ReadAllBytes($resourcePath)))
    {
        throw "The installed tool modified files during --check."
    }

    Invoke-NativeCommand $toolExecutable @("--all", $sourcePath, $resourcePath)
    $expectedSource = "class C`n{`n  void M()`n  {`n    if (true)`n    {`n      M();`n    }`n  }`n}`n"
    if ([IO.File]::ReadAllText($sourcePath) -cne $expectedSource)
    {
        throw "The installed C# formatter did not produce the expected whitespace."
    }

    [xml]$resource = [IO.File]::ReadAllText($resourcePath)
    $resourceNames = @($resource.root.data | ForEach-Object { $_.name }) -join ","
    $resourceValues = @($resource.root.data | ForEach-Object { $_.value }) -join ","
    if ($resourceNames -cne "a,b" -or $resourceValues -cne "a,b")
    {
        throw "The installed RESX formatter did not sort entries while retaining values."
    }

    Invoke-NativeCommand $toolExecutable @("--all", "--check", $sourcePath, $resourcePath)
    Write-Host "Validated installed tool: $toolVersionOutput"
}
finally
{
    $env:NUGET_PACKAGES = $originalNuGetPackages
    if (Test-Path -LiteralPath $validationRoot)
    {
        if ([IO.Path]::GetDirectoryName($validationRoot) -ne $temporaryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) -or
            -not [IO.Path]::GetFileName($validationRoot).StartsWith("PNFmt-tool-validation-", [StringComparison]::Ordinal))
        {
            throw "Refusing to remove a validation directory outside the temporary root: '$validationRoot'."
        }

        # Windows PowerShell also needs the extended path prefix for the
        # long assembly paths inside dotnet tool's package store.
        $cleanupPath = if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT)
        {
            if ($validationRoot.StartsWith("\\", [StringComparison]::Ordinal))
            {
                "\\?\UNC\" + $validationRoot.Substring(2)
            }
            else
            {
                "\\?\" + $validationRoot
            }
        }
        else
        {
            $validationRoot
        }

        Remove-Item -LiteralPath $cleanupPath -Recurse -Force
    }
}
