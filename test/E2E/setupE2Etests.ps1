#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

$arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
if ($IsWindows) {
    $FUNC_EXE_NAME = "func.exe"
    $os = "win"
} else {
    $FUNC_EXE_NAME = "func"
    if ($IsMacOS) {
        $os = "osx"
    } else {
        $os = "linux"
    }
}

$FUNC_CLI_DOWNLOAD_URL = "https://functionsclibuilds.blob.core.windows.net/builds/2/latest/Azure.Functions.Cli.$os-$arch.zip"
$FUNC_CLI_DIRECTORY = Join-Path $PSScriptRoot 'Azure.Functions.Cli'

Write-Host 'Deleting Functions Core Tools if exists...'
Remove-Item -Force "$FUNC_CLI_DIRECTORY.zip" -ErrorAction Ignore
Remove-Item -Recurse -Force $FUNC_CLI_DIRECTORY -ErrorAction Ignore

$version = Invoke-RestMethod -Uri 'https://functionsclibuilds.blob.core.windows.net/builds/2/latest/version.txt'
Write-Host "Downloading Functions Core Tools (Version: $version)..."

$output = "$FUNC_CLI_DIRECTORY.zip"
Invoke-RestMethod -Uri $FUNC_CLI_DOWNLOAD_URL -OutFile $output

Write-Host 'Extracting Functions Core Tools...'
Expand-Archive $output -DestinationPath $FUNC_CLI_DIRECTORY

Write-Host "Copying azure-functions-powershell-worker to  Functions Host workers directory..."

$configuration = if ($env:CONFIGURATION) { $env:CONFIGURATION } else { 'Debug' }
Remove-Item -Recurse -Force -Path "$FUNC_CLI_DIRECTORY/workers/powershell"
Copy-Item -Recurse -Force "$PSScriptRoot/../../src/bin/$configuration/netcoreapp2.1/publish/" "$FUNC_CLI_DIRECTORY/workers/powershell"

Write-Host "Staring Functions Host..."

$Env:AzureWebJobsScriptRoot = "$PSScriptRoot/TestFunctionApp"
$Env:FUNCTIONS_WORKER_RUNTIME = "powershell"
$Env:AZURE_FUNCTIONS_ENVIRONMENT = "development"
$Env:Path = "$Env:Path$([System.IO.Path]::PathSeparator)$FUNC_CLI_DIRECTORY"
$funcExePath = Join-Path $FUNC_CLI_DIRECTORY $FUNC_EXE_NAME

Start-Job -Name FuncJob -ArgumentList $funcExePath -ScriptBlock {
    Push-Location $Env:AzureWebJobsScriptRoot

    if ($IsMacOS -or $IsLinux) {
        chmod +x $args[0]
    }

    & $args[0] host start
}

Write-Host "Wait for Functions Host to start..."
Start-Sleep -s 10
