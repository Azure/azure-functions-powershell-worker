#!/usr/bin/env pwsh
#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

[CmdletBinding()]
param(
    [switch]
    $Clean,

    [switch]
    $Bootstrap,

    [switch]
    $Test,

    [switch]
    $NoBuild,

    [string]
    $Configuration = "Debug"
)

Import-Module "$PSScriptRoot/tools/helper.psm1"

# Bootstrap step
if ($Bootstrap.IsPresent) {
    Write-Log "Validate and install missing prerequisits for building ..."
    Install-Dotnet

    if (-not (Get-Module -Name PSDepend -ListAvailable)) {
        Write-Log -Warning "Module 'PSDepend' is missing. Installing 'PSDepend' ..."
        Install-Module -Name PSDepend -Scope CurrentUser -Force
    }
    if (-not (Get-Module -Name Pester -ListAvailable)) {
        Write-Log -Warning "Module 'Pester' is missing. Installing 'Pester' ..."
        Install-Module -Name Pester -Scope CurrentUser -Force
    }
}

# Clean step
if($Clean.IsPresent) {
    Push-Location $PSScriptRoot
    git clean -fdx
    Pop-Location
}

# Common step required by both build and test
Find-Dotnet

# Build step
if(!$NoBuild.IsPresent) {
    if (-not (Get-Module -Name PSDepend -ListAvailable)) {
        throw "Cannot find the 'PSDepend' module. Please specify '-Bootstrap' to install build dependencies."
    }

    # Generate csharp code from protobuf if needed
    New-gRPCAutoGenCode

    $requirements = "$PSScriptRoot/src/requirements.psd1"
    $modules = Import-PowerShellDataFile $requirements

    Write-Log "Install modules that are bundled with PowerShell Language worker, including"
    foreach ($entry in $modules.GetEnumerator()) {
        Write-Log -Indent "$($entry.Name) $($entry.Value.Version)"
    }

    Invoke-PSDepend -Path $requirements -Force
    dotnet publish --no-restore -c $Configuration $PSScriptRoot
    dotnet pack -c $Configuration "$PSScriptRoot/package"
}

# Test step
if($Test.IsPresent) {
    if (-not (Get-Module -Name Pester -ListAvailable)) {
        throw "Cannot find the 'Pester' module. Please specify '-Bootstrap' to install build dependencies."
    }

    dotnet test "$PSScriptRoot/test"

    if($env:APPVEYOR) {
        $res = Invoke-Pester "$PSScriptRoot/test/Modules" -OutputFormat NUnitXml -OutputFile TestsResults.xml -PassThru
        (New-Object 'System.Net.WebClient').UploadFile("https://ci.appveyor.com/api/testresults/nunit/$($env:APPVEYOR_JOB_ID)", (Resolve-Path .\TestsResults.xml))
        if ($res.FailedCount -gt 0) { throw "$($res.FailedCount) tests failed." }
    } else {
        Invoke-Pester "$PSScriptRoot/test/Modules"
    }
}
