[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$')]
    [string]$Version,

    [string]$CredentialTarget = "NuGet.ApiKey.PNFmt.Prod",
    [string]$ProjectPath = "PNFmt.Cli/PNFmt.Cli.csproj",
    [string]$SolutionPath = "PNFmt.slnx",
    [string]$PackagesDirectory = "artifacts/packages",
    [string]$Configuration = "Release",
    [string]$NuGetSource = "https://api.nuget.org/v3/index.json",
    [switch]$SkipPack,
    [switch]$SkipTests,
    [switch]$SkipPackageValidation,
    [switch]$PackOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-CredentialManagerSecret
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Target
    )

    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT)
    {
        throw "Windows Credential Manager is unavailable on this operating system. Set NUGET_API_KEY instead."
    }

    if (-not ("CredentialManagerNative" -as [Type]))
    {
        Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class CredentialManagerNative
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CREDENTIAL
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("Advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("Advapi32.dll", SetLastError = true)]
    public static extern void CredFree(IntPtr cred);
}
"@
    }

    $credentialPtr = [IntPtr]::Zero
    $credTypeGeneric = 1
    $result = [CredentialManagerNative]::CredRead($Target, $credTypeGeneric, 0, [ref]$credentialPtr)

    if (-not $result)
    {
        $win32Error = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "Credential target '$Target' was not found or could not be read (Win32 error: $win32Error)."
    }

    try
    {
        $credential = [Runtime.InteropServices.Marshal]::PtrToStructure(
            $credentialPtr,
            [Type][CredentialManagerNative+CREDENTIAL])

        if ($credential.CredentialBlobSize -le 0 -or $credential.CredentialBlob -eq [IntPtr]::Zero)
        {
            throw "Credential target '$Target' does not contain a secret."
        }

        $bytes = New-Object byte[] $credential.CredentialBlobSize
        [Runtime.InteropServices.Marshal]::Copy(
            $credential.CredentialBlob,
            $bytes,
            0,
            $credential.CredentialBlobSize)

        $secret = [Text.Encoding]::Unicode.GetString($bytes).TrimEnd([char]0)
        if ([string]::IsNullOrWhiteSpace($secret))
        {
            $secret = [Text.Encoding]::UTF8.GetString($bytes).TrimEnd([char]0)
        }

        if ([string]::IsNullOrWhiteSpace($secret))
        {
            throw "Credential target '$Target' was read but its secret is empty."
        }

        return $secret
    }
    finally
    {
        if ($credentialPtr -ne [IntPtr]::Zero)
        {
            [CredentialManagerNative]::CredFree($credentialPtr)
        }
    }
}

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

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Push-Location $repoRoot

try
{
    $resolvedProjectPath = (Resolve-Path (Join-Path $repoRoot $ProjectPath)).Path
    $resolvedSolutionPath = (Resolve-Path (Join-Path $repoRoot $SolutionPath)).Path
    $resolvedPackagesDirectory = [IO.Path]::GetFullPath(
        (Join-Path $repoRoot $PackagesDirectory))
    New-Item -ItemType Directory -Force -Path $resolvedPackagesDirectory | Out-Null

    $dotnetHome = Join-Path $repoRoot ".dotnet"
    New-Item -ItemType Directory -Force -Path $dotnetHome | Out-Null
    $env:DOTNET_CLI_HOME = $dotnetHome
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
    $env:DOTNET_NOLOGO = "1"
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"

    $packageId = (& dotnet msbuild $resolvedProjectPath -getProperty:PackageId -nologo | Out-String).Trim()
    if ($LASTEXITCODE -ne 0)
    {
        throw "Could not read PackageId from '$resolvedProjectPath'."
    }

    $toolCommandName = (
        & dotnet msbuild $resolvedProjectPath -getProperty:ToolCommandName -nologo |
            Out-String).Trim()
    if ($LASTEXITCODE -ne 0)
    {
        throw "Could not read ToolCommandName from '$resolvedProjectPath'."
    }

    if ([string]::IsNullOrWhiteSpace($packageId))
    {
        throw "PackageId is empty in '$resolvedProjectPath'."
    }

    if ([string]::IsNullOrWhiteSpace($toolCommandName))
    {
        throw "ToolCommandName is empty in '$resolvedProjectPath'."
    }

    $packagePath = Join-Path $resolvedPackagesDirectory "$packageId.$Version.nupkg"

    if (-not $SkipTests)
    {
        Write-Host "Running PNFmt tests and coverage checks..."
        & (Join-Path $PSScriptRoot "Test-Coverage.ps1") `
            -SolutionPath $resolvedSolutionPath -Configuration $Configuration
    }

    Write-Host "Running performance regression checks and benchmarks..."
    & (Join-Path $PSScriptRoot "Test-PerformanceReport.Tests.ps1")
    & (Join-Path $PSScriptRoot "Test-Performance.ps1")

    if (-not $SkipPack)
    {
        Write-Host "Packing '$packageId' version $Version..."
        Invoke-NativeCommand "dotnet" @(
            "pack",
            $resolvedProjectPath,
            "--configuration", $Configuration,
            "--output", $resolvedPackagesDirectory,
            "--nologo",
            "-p:Version=$Version"
        )
    }
    else
    {
        Write-Host "Skipping pack step as requested."
    }

    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf))
    {
        throw "Expected package was not found: '$packagePath'."
    }

    Write-Host "Selected package: $packagePath"

    if (-not $SkipPackageValidation)
    {
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
            $escapedSource = [Security.SecurityElement]::Escape($resolvedPackagesDirectory)
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
    }

    if ($PackOnly)
    {
        Write-Host "Pack-only mode enabled. Skipping push."
        Write-Output $packagePath
        return
    }

    $restoreApiKey = $false
    if ([string]::IsNullOrWhiteSpace($env:NUGET_API_KEY))
    {
        $env:NUGET_API_KEY = Get-CredentialManagerSecret -Target $CredentialTarget
        $restoreApiKey = $true
    }

    try
    {
        Write-Host "Pushing package to '$NuGetSource'..."
        Invoke-NativeCommand "dotnet" @(
            "nuget",
            "push",
            $packagePath,
            "--api-key", $env:NUGET_API_KEY,
            "--source", $NuGetSource
        )
    }
    finally
    {
        if ($restoreApiKey)
        {
            Remove-Item Env:NUGET_API_KEY -ErrorAction SilentlyContinue
        }
    }

    Write-Host "Package push completed."
    Write-Output $packagePath
}
finally
{
    Pop-Location
}
