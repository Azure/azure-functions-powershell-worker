#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

using namespace System.Runtime.InteropServices

$IsWindowsEnv = [RuntimeInformation]::IsOSPlatform([OSPlatform]::Windows)

$arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
if ($IsWindowsEnv) {
    $os = "win"
    $FUNC_EXE_NAME = "func.exe"
} elseif ($IsMacOS) {
    $os = "osx"
    $FUNC_EXE_NAME = "func"
} else {
    $os = "linux"
    $FUNC_EXE_NAME = "func"
}
$FUNC_CLI_DOWNLOAD_URL = "https://functionsclibuilds.blob.core.windows.net/builds/2/latest/Azure.Functions.Cli.$os-$arch.zip"
$FUNC_CLI_DIRECTORY = Join-Path $PSScriptRoot 'Azure.Functions.Cli'

Write-Host 'Deleting Functions Core Tools if exists...'
Remove-Item -Force "$FUNC_CLI_DIRECTORY.zip" -ErrorAction Ignore
Remove-Item -Recurse -Force $FUNC_CLI_DIRECTORY -ErrorAction Ignore

Write-Host 'Downloading Functions Core Tools...'
Invoke-RestMethod -Uri 'https://functionsclibuilds.blob.core.windows.net/builds/2/latest/version.txt' -OutFile version.txt
Write-Host "Using version: $(Get-Content -Raw version.txt)"
Remove-Item version.txt

$output = "$FUNC_CLI_DIRECTORY.zip"
Invoke-RestMethod -Uri $FUNC_CLI_DOWNLOAD_URL -OutFile $output

Write-Host 'Extracting Functions Core Tools...'
Expand-Archive $output -DestinationPath $FUNC_CLI_DIRECTORY

Write-Host "Copying azure-functions-powershell-worker to  Functions Host workers directory..."

$configuration = if ($env:CONFIGURATION) { $env:CONFIGURATION } else { 'Debug' }
Copy-Item -Recurse -Force "$PSScriptRoot/../../src/bin/$configuration/netcoreapp2.1/publish/" "$FUNC_CLI_DIRECTORY/workers/powershell"


Write-Host "Staring Functions Host..."

$Env:AzureWebJobsScriptRoot = "$PSScriptRoot/TestFunctionApp"
$Env:FUNCTIONS_WORKER_RUNTIME = "powershell"
$Env:AZURE_FUNCTIONS_ENVIRONMENT = "development"
$Env:Path = "$Env:Path$([System.IO.Path]::PathSeparator)$FUNC_CLI_DIRECTORY"

Start-Job -Name FuncJob -ArgumentList (Join-Path $FUNC_CLI_DIRECTORY $FUNC_EXE_NAME) -ScriptBlock {
    Push-Location $Env:AzureWebJobsScriptRoot

    if ($IsMacOS -or $IsLinux) {
        chmod +x $args[0]
    }

    & $args[0] host start
}

Write-Host "Wait for Functions Host to start..."
Start-Sleep -s 10
