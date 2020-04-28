#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

using namespace System.Runtime.InteropServices

$IsWindowsEnv = [RuntimeInformation]::IsOSPlatform([OSPlatform]::Windows)
$RepoRoot = (Resolve-Path "$PSScriptRoot/..").Path
$MinimalSDKVersion = '2.1.805'
$DefaultSDKVersion = '2.1.805'
$LocalDotnetDirPath = if ($IsWindowsEnv) { "$env:LocalAppData\Microsoft\dotnet" } else { "$env:HOME/.dotnet" }
$GrpcToolsVersion = '2.27.0' # grpc.tools
$GoogleProtobufToolsVersion = '3.11.4' # google.protobuf.tools

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

function Get-VersionCore($Version) {
    if ($Version -match '^\d+\.\d+\.\d+') {
        $Matches.0
    } else {
        throw "Unexpected version: '$Version'"
    }
}

function Test-DotnetSDK
{
    param($dotnetExePath)

    if (Test-Path $dotnetExePath) {
        $installedVersion = Get-VersionCore (& $dotnetExePath --version)
        return [version]$installedVersion -ge [version]$MinimalSDKVersion
    }
    return $false
}

function Install-Dotnet {
    [CmdletBinding()]
    param(
        [string]$Channel = 'release',
        [string]$Version = $DefaultSDKVersion
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

    if ($Force -or -not (Test-Path "$RepoRoot/src/Messaging/protobuf/FunctionRpc.cs") -or
                   -not (Test-Path "$RepoRoot/src/Messaging/protobuf/FunctionRpcGrpc.cs"))
    {
        Write-Log "Generate the CSharp code for gRPC communication from protobuf"

        Resolve-ProtoBufToolPath

        $outputDir = "$RepoRoot/src/Messaging/protobuf"
        Remove-Item $outputDir -Recurse -Force -ErrorAction SilentlyContinue
        New-Item $outputDir -ItemType Directory -Force -ErrorAction Stop > $null

        $allProtoFiles = Get-ChildItem -Path $Script:protobuf_dir_Path -Filter "*.proto" -Recurse
        $fileList = $allProtoFiles.FullName

        & $Script:protoc_Path $fileList `
            --csharp_out $outputDir `
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
            Resolve-Path "$nugetPath/grpc.tools/$GrpcToolsVersion/tools/$plat_arch_Name/$protoc_Name" |
            ForEach-Object Path

        if (-not $Script:protoc_Path) {
            throw "Couldn't find the executable 'protoc'. Check if the package 'grpc.tools' has been restored."
        }

        $Script:grpc_csharp_plugin_Path =
            Resolve-Path "$nugetPath/grpc.tools/$GrpcToolsVersion/tools/$plat_arch_Name/$grpc_csharp_plugin_Name" |
            ForEach-Object Path

        if (-not $Script:grpc_csharp_plugin_Path) {
            throw "Couldn't find the executable 'grpc_csharp_plugin'. Check if the package 'grpc.tools' has been restored."
        }

        $Script:google_protobuf_tools_Path =
            Resolve-Path "$nugetPath/google.protobuf.tools/$GoogleProtobufToolsVersion/tools" |
            ForEach-Object Path

        if (-not $Script:google_protobuf_tools_Path) {
            throw "Couldn't find the protobuf tools. Check if the package 'google.protobuf.tools' has been restored."
        }

        $Script:protobuf_dir_Path = "$RepoRoot/protobuf/src/proto"
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

#region Start-ResGen

$generated_code_template = @'
//------------------------------------------------------------------------------
// <auto-generated>
//   This code was generated by a running 'Start-ResGen' from tools\helper.psm1.
//   To add or remove a member, edit your .resx file then rerun 'Start-ResGen'.
//
//   Changes to this file may cause incorrect behavior and will be lost if
//   the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Reflection;

/// <summary>
/// A strongly-typed resource class, for looking up localized strings, etc.
/// </summary>
internal class {0} {{

    private static global::System.Resources.ResourceManager resourceMan;
    private static global::System.Globalization.CultureInfo resourceCulture;

    /// <summary>
    /// Returns the cached ResourceManager instance used by this class.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
    internal static global::System.Resources.ResourceManager ResourceManager {{
        get {{
            if (object.ReferenceEquals(resourceMan, null)) {{
                global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Microsoft.Azure.Functions.PowerShellWorker.resources.{0}", typeof({0}).Assembly);
                resourceMan = temp;
            }}

            return resourceMan;
        }}
    }}

    /// <summary>
    /// Overrides the current threads CurrentUICulture property for all
    /// resource lookups using this strongly typed resource class.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
    internal static global::System.Globalization.CultureInfo Culture {{
        get {{ return resourceCulture; }}
        set {{ resourceCulture = value; }}
    }}

    {1}
}}
'@

$individual_resource_string_proprety = @'

    /// <summary>
    /// Looks up a localized string similar to 
    ///   {0}
    /// </summary>
    internal static string {1} {{
        get {{
            return ResourceManager.GetString("{1}", resourceCulture);
        }}
    }}

'@

function Start-ResGen
{
    param([switch] $Force)

    $sourceDir = (Resolve-Path -Path "$PSScriptRoot/../src").Path
    $genDir = Join-Path -Path $sourceDir -ChildPath gen

    if (Test-Path -Path $genDir -PathType Container) {
        if ($Force) {
            Remove-Item -Path $genDir -Recurse -Force
        } else {
            return
        }
    }

    $resourceDir = Join-Path -Path $sourceDir -ChildPath resources
    $resxFiles = Get-ChildItem -Path $resourceDir -Filter *.resx
    $null = New-Item -Path $genDir -ItemType Directory -Force

    foreach ($resx in $resxFiles) {
        $typeName = [System.IO.Path]::GetFileNameWithoutExtension($resx.FullName)
        $resXml = [xml] (Get-Content $resx.FullName)
        $properties = [System.Text.StringBuilder]::new()
        foreach ($data in $resXml.root.data) {
            $name = $data.name
            $value = $data.value -replace "`n","\n"

            $property = $individual_resource_string_proprety -f $value, $name
            $null = $properties.Append($property)
        }
        $typeCode = $generated_code_template -f $typeName, $properties.ToString()
        $typeFile = Join-Path -Path $genDir "$typeName.cs"
        Set-Content -Path $typeFile -Value $typeCode
    }
}

#endregion Start-ResGen
