#!/usr/bin/env pwsh
#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [Parameter()]
    [switch]
    $Bootstrap,

    [Parameter()]
    [switch]
    $Clean,

    [Parameter()]
    [switch]
    $Test
)

$NeededTools = @{
    DotnetSdk = ".NET SDK latest"
    PowerShellGet = "PowerShellGet latest"
    InvokeBuild = "InvokeBuild latest"
}

function needsDotnetSdk () {
    try {
        $opensslVersion = (dotnet --version)
    } catch {
        return $true
    }
    return $false
}

function needsPowerShellGet () {
    $modules = Get-Module -ListAvailable -Name PowerShellGet | Where-Object Version -gt 1.6.0
    if ($modules.Count -gt 0) {
        return $false
    }
    return $true
}

function needsInvokeBuild () {
    if (Get-Module -ListAvailable -Name InvokeBuild) {
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
    if (needsInvokeBuild) {
        $missingTools += $NeededTools.InvokeBuild
    }

    return $missingTools
}

function hasMissingTools () {
    return ((getMissingTools).Count -gt 0)
}

if ($Bootstrap) {
    $string = "Here is what your environment is missing:`n"
    $missingTools = getMissingTools
    if (($missingTools).Count -eq 0) {
        $string += "* nothing!`n`n Run this script without a flag to build or a -Clean to clean."
    } else {
        $missingTools | ForEach-Object {$string += "* $_`n"}
    }
    Write-Host "`n$string`n"
} elseif(hasMissingTools) {
    Write-Host "You are missing needed tools. Run './build.ps1 -Bootstrap' to see what they are."
} else {
    if($Clean) {
        Invoke-Build Clean
    }

    Invoke-Build Build

    if($Test) {
        Invoke-Build Test
    }
}
