#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

using namespace System.Runtime.InteropServices

$IsWindowsEnv = [RuntimeInformation]::IsOSPlatform([OSPlatform]::Windows)
$MinimalSDKVersion = '2.1.300'

function Find-Dotnet
{
    param([switch] $NoThrow)

    $originalPath = $env:PATH
    $dotnetPath = if ($IsWindowsEnv) { "$env:LocalAppData\Microsoft\dotnet" } else { "$env:HOME/.dotnet" }

    # If there dotnet is already in the PATH, check to see if that version of dotnet can find the required SDK.
    # This is "typically" the globally installed dotnet.
    if (Get-Command 'dotnet' -ErrorAction SilentlyContinue) {
        $installedVersion = (dotnet --version)
        if ($installedVersion -lt $MinimalSDKVersion) {
            # Globally installed dotnet doesn't have the required SDK version, prepend the user local dotnet location.
            Write-Warning "Can't find the dotnet SDK version $MinimalSDKVersion or higher, prepending '$dotnetPath' to PATH."
            $env:PATH = $dotnetPath + [IO.Path]::PathSeparator + $env:PATH
        }
    }
    else {
        Write-Warning "Could not find 'dotnet', appending $dotnetPath to PATH."
        $env:PATH += [IO.Path]::PathSeparator + $dotnetPath
    }

    if (-not (Get-Command 'dotnet' -ErrorAction SilentlyContinue)) {
        # Restore the original PATH.
        $env:PATH = $originalPath
        if (-not $NoThrow) {
            throw "Cannot find the dotnet SDK for .NET Core 2.1. Please specify '-Bootstrap' to install build dependencies."
        }
    }
}

function Test-DotnetSDK
{
    Find-Dotnet -NoThrow

    $dotnet = Get-Command 'dotnet' -ErrorAction SilentlyContinue
    $dotnetSDKExists = $null -ne $dotnet
    $dotnetSDKVersion = if ($dotnetSDKExists) { dotent --version }

    if ($dotnetSDKExists -and $dotnetSDKVersion -gt $MinimalSDKVersion) {
        Write-Log "dotnet SDK $dotnetSDKVersion is found."
        return $true
    }

    $logMsg = if (-not $dotnetSDKExists) {
        "dotent SDK is not present. Installing dotnet SDK."
    } else {
        "dotnet SDK out of date. Require '$MinimalSDKVersion' but found '$dotnetSDKVersion'. Updating dotnet."
    }

    Write-Log $logMsg -Warning
    return $false;
}

function Install-Dotnet {
    [CmdletBinding()]
    param(
        [string]$Channel = 'release',
        [string]$Version = '2.1.401'
    )

    $obtainUrl = "https://raw.githubusercontent.com/dotnet/cli/master/scripts/obtain"

    try {
        if ($IsWindowsEnv) {
            Remove-Item "$env:LocalAppData\Microsoft\dotnet" -Recurse -Force -ErrorAction SilentlyContinue
            $installScript = "dotnet-install.ps1"
            Invoke-WebRequest -Uri $obtainUrl/$installScript -OutFile $installScript
            & .\$installScript -Channel $Channel -Version $Version
        }
        else {
            Remove-Item "$env:HOME/.dotnet" -Recurse -Force -ErrorAction SilentlyContinue
            $installScript = "dotnet-install.sh"
            Invoke-WebRequest -Uri $obtainUrl/$installScript -OutFile $installScript
            bash ./$installScript -c $Channel -v $Version
        }
    }
    finally {
        Remove-Item $installScript -Force -ErrorAction SilentlyContinue
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
