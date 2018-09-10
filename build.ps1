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
    PowerShellGet = "PowerShellGet latest"
}

function needsDotnetSdk () {
    try {
        return ((dotnet --version) -lt 2.1)
    } catch {
        return $true
    }
    return $false
}

function needsPowerShellGet () {
    $modules = Get-Module -ListAvailable -Name PowerShellGet | Where-Object Version -ge 1.6.0
    if ($modules.Count -gt 0) {
        return $false
    }
    return $true
}

function getMissingTools () {
    $missingTools = @()

    if (needsDotnetSdk) {
        $missingTools += $NeededTools.DotnetSdk
    }
    if (needsPowerShellGet) {
        $missingTools += $NeededTools.PowerShellGet
    }

    return $missingTools
}

function hasMissingTools () {
    return ((getMissingTools).Count -gt 0)
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
dotnet build -c $Configuration
dotnet publish -c $Configuration
Push-Location package
dotnet pack -c $Configuration
Pop-Location

# Test step
if($Test) {
    Push-Location test
    dotnet test
    Set-Location Modules
    Invoke-Pester
    Pop-Location
}

# Return to the original directory
Pop-Location
