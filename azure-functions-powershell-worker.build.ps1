#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [string[]]
    $Tasks
)

if ($MyInvocation.ScriptName -notlike '*Invoke-Build.ps1') {
    Invoke-Build $Tasks $MyInvocation.MyCommand.Path @PSBoundParameters
    return
}

$BuildRoot = $PSScriptRoot

task Clean {
    exec { dotnet clean }
    Remove-Item -Recurse -Force src/bin -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force src/obj -ErrorAction SilentlyContinue

    # Remove the built nuget package
    Remove-Item -Recurse -Force package/bin -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force package/obj -ErrorAction SilentlyContinue
}

task Build {
    exec { dotnet build }
    exec { dotnet publish }
    Set-Location package
    exec { dotnet pack }
}

task Test {
    Set-Location test
    exec { dotnet test }
    Set-Location Modules
    Invoke-Pester
}

task . Clean, Build, Test