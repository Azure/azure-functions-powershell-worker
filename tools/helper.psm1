#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

using namespace System.Runtime.InteropServices

$IsWindowsEnv = [RuntimeInformation]::IsOSPlatform([OSPlatform]::Windows)
$RepoRoot = (Resolve-Path "$PSScriptRoot/..").Path
$MinimalSDKVersion = '2.1.300'
$LocalDotnetDirPath = if ($IsWindowsEnv) { "$env:LocalAppData\Microsoft\dotnet" } else { "$env:HOME/.dotnet" }

function Find-Dotnet
{
    $dotnetFile = if ($IsWindowsEnv) { "dotnet.exe" } else { "dotnet" }
    $dotnetExePath = Join-Path -Path $LocalDotnetDirPath -ChildPath $dotnetFile

    # If dotnet is already in the PATH, check to see if that version of dotnet can find the required SDK.
    # This is "typically" the globally installed dotnet.
    $foundDotnetWithRightVersion = $false
    $dotnetInPath = Get-Command 'dotnet' -ErrorAction SilentlyContinue
    if ($dotnetInPath) {
        $foundDotnetWithRightVersion = Test-DotnetSDK $dotnetInPath.Source
    }

    if (-not $foundDotnetWithRightVersion) {
        if (Test-DotnetSDK $dotnetExePath) {
            Write-Warning "Can't find the dotnet SDK version $MinimalSDKVersion or higher, prepending '$LocalDotnetDirPath' to PATH."
            $env:PATH = $LocalDotnetDirPath + [IO.Path]::PathSeparator + $env:PATH
        }
        else {
            throw "Cannot find the dotnet SDK for .NET Core 2.1. Please specify '-Bootstrap' to install build dependencies."
        }
    }
}

function Test-DotnetSDK
{
    param($dotnetExePath)

    if (Test-Path $dotnetExePath) {
        $installedVersion = & $dotnetExePath --version
        return $installedVersion -ge $MinimalSDKVersion
    }
    return $false
}

function Install-Dotnet {
    [CmdletBinding()]
    param(
        [string]$Channel = 'release',
        [string]$Version = '2.2.102'
    )

    try {
        Find-Dotnet
        return  # Simply return if we find dotnet SDk with the correct version
    } catch { }

    $logMsg = if (Get-Command 'dotnet' -ErrorAction SilentlyContinue) {
        "dotent SDK is not present. Installing dotnet SDK."
    } else {
        "dotnet SDK out of date. Require '$MinimalSDKVersion' but found '$dotnetSDKVersion'. Updating dotnet."
    }
    Write-Log $logMsg -Warning

    $obtainUrl = "https://raw.githubusercontent.com/dotnet/cli/master/scripts/obtain"

    try {
        Remove-Item $LocalDotnetDirPath -Recurse -Force -ErrorAction SilentlyContinue
        $installScript = if ($IsWindowsEnv) { "dotnet-install.ps1" } else { "dotnet-install.sh" }
        Invoke-WebRequest -Uri $obtainUrl/$installScript -OutFile $installScript

        if ($IsWindowsEnv) {
            & .\$installScript -Channel $Channel -Version $Version
        } else {
            bash ./$installScript -c $Channel -v $Version
        }
    }
    finally {
        Remove-Item $installScript -Force -ErrorAction SilentlyContinue
    }
}

function New-gRPCAutoGenCode
{
    [CmdletBinding()]
    param(
        [switch] $Force
    )

    if ($Force -or -not (Test-Path "$RepoRoot/src/Messaging/FunctionRpc.cs") -or
                   -not (Test-Path "$RepoRoot/src/Messaging/FunctionRpcGrpc.cs"))
    {
        Write-Log "Generate the CSharp code for gRPC communication from protobuf"

        Resolve-ProtoBufToolPath

        $outputDir = "$RepoRoot/src/Messaging"
        Remove-Item "$outputDir/FunctionRpc*.cs" -Force -ErrorAction SilentlyContinue

        & $Script:protoc_Path $Script:protobuf_file_Path --csharp_out $outputDir `
                                                         --grpc_out=$outputDir `
                                                         --plugin=protoc-gen-grpc=$Script:grpc_csharp_plugin_Path `
                                                         --proto_path=$Script:protobuf_dir_Path `
                                                         --proto_path=$Script:google_protobuf_tools_Path
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to generate the CSharp code for gRPC communication."
        }
    }
}

function Resolve-ProtoBufToolPath
{
    if (-not $Script:protoc_Path) {
        Write-Log "Resolve the protobuf tools for auto-generating code"
        $nugetPath = "~/.nuget/packages"
        $toolsPath = "$RepoRoot/tools"

        if (-not (Test-Path "$toolsPath/obj/project.assets.json")) {
            dotnet restore $toolsPath --verbosity quiet
            if ($LASTEXITCODE -ne 0) {
                throw "Cannot resolve protobuf tools. 'dotnet restore $toolsPath' failed."
            }
        }

        if ($IsWindowsEnv) {
            $plat_arch_Name = "windows_x64"
            $protoc_Name = "protoc.exe"
            $grpc_csharp_plugin_Name = "grpc_csharp_plugin.exe"
        } else {
            $plat_arch_Name = if ($IsLinux) { "linux_x64" } else { "macosx_x64" }
            $protoc_Name = "protoc"
            $grpc_csharp_plugin_Name = "grpc_csharp_plugin"
        }

        $Script:protoc_Path =
            Get-ChildItem "$nugetPath/grpc.tools/*/$protoc_Name" -Recurse |
            Where-Object FullName -Like "*$plat_arch_Name*" |
            Sort-Object -Property FullName -Descending |
            Select-Object -First 1 | ForEach-Object FullName

        if (-not $Script:protoc_Path) {
            throw "Couldn't find the executable 'protoc'. Check if the package 'grpc.tools' has been restored."
        }

        $Script:grpc_csharp_plugin_Path =
            Get-ChildItem "$nugetPath/grpc.tools/*/$grpc_csharp_plugin_Name" -Recurse |
            Where-Object FullName -Like "*$plat_arch_Name*" |
            Sort-Object -Property FullName -Descending |
            Select-Object -First 1 | ForEach-Object FullName

        if (-not $Script:grpc_csharp_plugin_Path) {
            throw "Couldn't find the executable 'grpc_csharp_plugin'. Check if the package 'grpc.tools' has been restored."
        }

        $Script:google_protobuf_tools_Path =
            Get-ChildItem "$nugetPath/google.protobuf.tools/*/tools" |
            Sort-Object -Property FullName -Descending |
            Select-Object -First 1 | ForEach-Object FullName

        if (-not $Script:google_protobuf_tools_Path) {
            throw "Couldn't find the protobuf tools. Check if the package 'google.protobuf.tools' has been restored."
        }

        $Script:protobuf_dir_Path = "$RepoRoot/protobuf/src/proto"
        $Script:protobuf_file_Path = "$Script:protobuf_dir_Path/FunctionRpc.proto"
    }
}

function Get-WebFile {
    param (
        [string] $Url,
        [string] $OutFile
    )
    $directoryName = [System.IO.Path]::GetDirectoryName($OutFile)
    if (!(Test-Path $directoryName)) {
        New-Item -Type Directory $directoryName
    }
    Remove-Item $OutFile -ErrorAction SilentlyContinue
    Invoke-RestMethod $Url -OutFile $OutFile
}

function Invoke-Tests
{
    param(
        [string] $Path,
        [string] $OutputFile
    )

    if($env:APPVEYOR) {
        $res = Invoke-Pester $Path -OutputFormat NUnitXml -OutputFile $OutputFile -PassThru
        (New-Object 'System.Net.WebClient').UploadFile("https://ci.appveyor.com/api/testresults/nunit/$($env:APPVEYOR_JOB_ID)", (Resolve-Path $OutputFile))
        if ($res.FailedCount -gt 0) { throw "$($res.FailedCount) tests failed." }
    } else {
        Invoke-Pester $Path
    }
}

function Write-Log
{
    param(
        [string] $Message,
        [switch] $Warning,
        [switch] $Indent
    )

    $foregroundColor = if ($Warning) { "Yellow" } else { "Green" }
    $indentPrefix = if ($Indent) { "    " } else { "" }
    Write-Host -ForegroundColor $foregroundColor "${indentPrefix}${Message}"
}
