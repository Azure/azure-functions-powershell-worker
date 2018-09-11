#!/usr/bin/env pwsh
#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [Parameter()]
    [switch]
    $Clean,

    [Parameter()]
    [switch]
    $Test,

    [Parameter()]
    [string]
    $Configuration = "Debug"
)

$NeededTools = @{
    DotnetSdk = ".NET SDK latest"
}

function needsDotnetSdk () {
    try {
        return ((dotnet --version) -lt 2.1)
    } catch {
        return $true
    }
    return $false
}

function getMissingTools () {
    $missingTools = @()

    if (needsDotnetSdk) {
        $missingTools += $NeededTools.DotnetSdk
    }

    return $missingTools
}

$missingTools = getMissingTools
if ($missingTools.Count -gt 0) {
    $string = "Here is what your environment is missing:`n"
    $missingTools = getMissingTools
    if (($missingTools).Count -eq 0) {
        $string += "* nothing!`n`n Run this script without a flag to build or a -Clean to clean."
    } else {
        $missingTools | ForEach-Object {$string += "* $_`n"}
    }
    Write-Host "`n$string`n"
    return
}

# Start at the root of the directory
Push-Location $PSScriptRoot

# Clean step
if($Clean) {
    git clean -fdx
}

# Build step

# Install using PSDepend if it's available, otherwise use the backup script
if ((Get-Module -ListAvailable -Name PSDepend).Count -gt 0) {
    Invoke-PSDepend -Force
} else {
    & "$PSScriptRoot/../tools/InstallDependencies.ps1"
}

dotnet build -c $Configuration
dotnet publish -c $Configuration

Push-Location package
dotnet pack -c $Configuration
Pop-Location

# Test step
if($Test) {
    Push-Location test
    dotnet test
    Invoke-Pester Modules
    Pop-Location
}

# Return to the original directory
Pop-Location
