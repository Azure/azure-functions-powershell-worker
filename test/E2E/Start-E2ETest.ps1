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

if (-not $env:CORE_TOOLS_URL)
{
    $env:CORE_TOOLS_URL = 'https://functionsclibuilds.blob.core.windows.net/builds/2/latest'
}

$version = Invoke-RestMethod -Uri "$env:CORE_TOOLS_URL/version.txt"
Write-Host "Downloading Functions Core Tools (Version: $version)..."

$output = "$FUNC_CLI_DIRECTORY.zip"
Invoke-RestMethod -Uri $FUNC_CLI_DOWNLOAD_URL -OutFile $output

Write-Host 'Extracting Functions Core Tools...'
Expand-Archive $output -DestinationPath $FUNC_CLI_DIRECTORY

Write-Host "Copying azure-functions-powershell-worker to Functions Host workers directory..."

$configuration = if ($env:CONFIGURATION) { $env:CONFIGURATION } else { 'Debug' }
Remove-Item -Recurse -Force -Path "$FUNC_CLI_DIRECTORY/workers/powershell"
Copy-Item -Recurse -Force "$PSScriptRoot/../../src/bin/$configuration/netcoreapp3.1/publish/" "$FUNC_CLI_DIRECTORY/workers/powershell"

Write-Host "Staring Functions Host..."

$Env:FUNCTIONS_WORKER_RUNTIME = "powershell"
$Env:AZURE_FUNCTIONS_ENVIRONMENT = "development"
$Env:Path = "$Env:Path$([System.IO.Path]::PathSeparator)$FUNC_CLI_DIRECTORY"
$funcExePath = Join-Path $FUNC_CLI_DIRECTORY $FUNC_EXE_NAME

Write-Host "Installing extensions..."
Push-Location "$PSScriptRoot\TestFunctionApp"

if ($IsMacOS -or $IsLinux) {
    chmod +x $funcExePath
}

& $funcExePath extensions install | ForEach-Object {    
  if ($_ -match 'OK')    
  { Write-Host $_ -f Green }    
  elseif ($_ -match 'FAIL|ERROR')   
  { Write-Host $_ -f Red }   
  else    
  { Write-Host $_ }    
}

if ($LASTEXITCODE -ne 0) { throw "Installing extensions failed." }
Pop-Location

Write-Host "Running E2E integration tests..." -ForegroundColor Green
Write-Host "-----------------------------------------------------------------------------`n" -ForegroundColor Green

dotnet test "$PSScriptRoot/Azure.Functions.PowerShellWorker.E2E/Azure.Functions.PowerShellWorker.E2E/Azure.Functions.PowerShellWorker.E2E.csproj" --logger:trx --results-directory "$PSScriptRoot/../../testResults"
if ($LASTEXITCODE -ne 0) { throw "xunit tests failed." }

Write-Host "-----------------------------------------------------------------------------" -ForegroundColor Green
