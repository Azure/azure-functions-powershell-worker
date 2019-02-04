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

Import-Module "$PSScriptRoot/tools/helper.psm1" -Force

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

    # Generate C# files for resources
    Start-ResGen

    # Generate csharp code from protobuf if needed
    New-gRPCAutoGenCode

    $requirements = "$PSScriptRoot/src/requirements.psd1"
    $modules = Import-PowerShellDataFile $requirements

    Write-Log "Install modules that are bundled with PowerShell Language worker, including"
    foreach ($entry in $modules.GetEnumerator()) {
        Write-Log -Indent "$($entry.Name) $($entry.Value.Version)"
    }

    Invoke-PSDepend -Path $requirements -Force

    # TODO: Remove this once the SDK properly bundles modules
    Get-WebFile -Url 'https://raw.githubusercontent.com/PowerShell/PowerShell/master/src/Modules/Windows/Microsoft.PowerShell.Utility/Microsoft.PowerShell.Utility.psd1' `
        -OutFile "$PSScriptRoot/src/Modules/Microsoft.PowerShell.Utility/Microsoft.PowerShell.Utility.psd1"
    Get-WebFile -Url 'https://raw.githubusercontent.com/PowerShell/PowerShell/master/src/Modules/Windows/Microsoft.PowerShell.Management/Microsoft.PowerShell.Management.psd1' `
        -OutFile "$PSScriptRoot/src/Modules/Microsoft.PowerShell.Management/Microsoft.PowerShell.Management.psd1" 

    # Compile project
    Write-Log "Compiling project..."
    dotnet publish -c $Configuration $PSScriptRoot
    if ($LASTEXITCODE -ne 0) { throw "Build compilation failed." }

    # Remove the fullCLR folder under Modules\PackageManagement (since it has the same content at the coreclr folder)
    $packageManagementFullCLRFolderPath = (Get-Item "$PSScriptRoot/src/bin/$Configuration/*/Modules/PackageManagement/fullclr" -ErrorAction SilentlyContinue).FullName
    if (Test-Path $packageManagementFullCLRFolderPath)
    {
        Write-Log "Deleting PackageManagement/fullclr folder..."
        Remove-Item $packageManagementFullCLRFolderPath -Force -Recurse -ErrorAction Stop
        Write-Log "    Completed."
    }

    # Create package
    Write-Log "Creating package..."
    dotnet pack -c $Configuration "$PSScriptRoot/package"
    if ($LASTEXITCODE -ne 0) { throw "Package creation failed." }
}

# Test step
if($Test.IsPresent) {
    if (-not (Get-Module -Name Pester -ListAvailable)) {
        throw "Cannot find the 'Pester' module. Please specify '-Bootstrap' to install build dependencies."
    }

    dotnet test "$PSScriptRoot/test/Unit"
    if ($LASTEXITCODE -ne 0) { throw "xunit tests failed." }

    Invoke-Tests -Path "$PSScriptRoot/test/Unit/Modules" -OutputFile UnitTestsResults.xml
}
