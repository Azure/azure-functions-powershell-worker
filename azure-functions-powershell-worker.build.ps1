#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [string[]]
    $Tasks,
    [string]
    $Configuration = 'Debug'
)

if ($MyInvocation.ScriptName -notlike '*Invoke-Build.ps1') {
    Invoke-Build $Tasks $MyInvocation.MyCommand.Path @PSBoundParameters
    return
}

$BuildRoot = $PSScriptRoot

task Clean {
    exec { git clean -fdx }
}

task Build {
    exec { dotnet build -c $Configuration }
    exec { dotnet publish -c $Configuration }
    Set-Location package
    exec { dotnet pack -c $Configuration }
}

task Test {
    Set-Location test
    exec { dotnet test }
    Set-Location Modules
    Invoke-Pester
}

task . Clean, Build, Test
