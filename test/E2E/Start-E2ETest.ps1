#	
# Copyright (c) Microsoft. All rights reserved.	
# Licensed under the MIT license. See LICENSE file in the project root for full license information.	
#
param
(
    [Switch]
    $UseCoreToolsBuildFromIntegrationTests
)

function NewTaskHubName
{
    param(
        [int]$Length = 45
    )

    <#
    Task hubs are identified by a name that conforms to these rules:
      - Contains only alphanumeric characters
      - Starts with a letter
      - Has a minimum length of 3 characters, maximum length of 45 characters

    doc: According to the documentation here https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-task-hubs?tabs=csharp
    #>

    $min = 20
    $max = 45

    if ($length -lt $min -or $Length -gt $max)
    {
        throw "Length must be between $min and $max characters. Provided value: $length"
    }

    $letters = 'a'..'z' + 'A'..'Z'
    $numbers = 0..9
    $alphanumeric = $letters + $numbers

    # First value is a letter
    $sb = [System.Text.StringBuilder]::new()
    $value = $letters | Get-Random
    $sb.Append($value) | Out-Null

    # Add the date and time as part of the name. This way, we can delete older versions.
    # Example: 202104251929 is for 2021-04-25:1929 (this value is 12 characters long)
    $value = Get-Date -Format "yyyyMMddHHmm"
    $sb.Append($value) | Out-Null

    # The remaining of the characters are random alphanumeric values
    for ($index = 13; $index -lt $length; $index++)
    {
        $value = $alphanumeric | Get-Random
        $sb.Append($value) | Out-Null
    }

    $sb.ToString()
}

$taskHubName = NewTaskHubName -Length 45

$FUNC_RUNTIME_VERSION = '3'
$TARGET_FRAMEWORK = 'net5.0'
$POWERSHELL_VERSION = '7.2'

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

$coreToolsDownloadURL = $null
if ($UseCoreToolsBuildFromIntegrationTests.IsPresent)
{
    $coreToolsDownloadURL = "https://functionsintegclibuilds.blob.core.windows.net/builds/$FUNC_RUNTIME_VERSION/latest/Azure.Functions.Cli.$os-$arch.zip"
    $env:CORE_TOOLS_URL = "https://functionsintegclibuilds.blob.core.windows.net/builds/$FUNC_RUNTIME_VERSION/latest"
}
else
{
    $coreToolsDownloadURL = "https://functionsclibuilds.blob.core.windows.net/builds/$FUNC_RUNTIME_VERSION/latest/Azure.Functions.Cli.$os-$arch.zip"
    if (-not $env:CORE_TOOLS_URL)
    {
        $env:CORE_TOOLS_URL = "https://functionsclibuilds.blob.core.windows.net/builds/$FUNC_RUNTIME_VERSION/latest"
    }
}

$FUNC_CLI_DIRECTORY = Join-Path $PSScriptRoot 'Azure.Functions.Cli'

Write-Host 'Deleting Functions Core Tools if exists...'
Remove-Item -Force "$FUNC_CLI_DIRECTORY.zip" -ErrorAction Ignore
Remove-Item -Recurse -Force $FUNC_CLI_DIRECTORY -ErrorAction Ignore

$version = Invoke-RestMethod -Uri "$env:CORE_TOOLS_URL/version.txt"
Write-Host "Downloading Functions Core Tools (Version: $version)..."

$output = "$FUNC_CLI_DIRECTORY.zip"
Invoke-RestMethod -Uri $coreToolsDownloadURL -OutFile $output

Write-Host 'Extracting Functions Core Tools...'
Expand-Archive $output -DestinationPath $FUNC_CLI_DIRECTORY

if (-not $UseCoreToolsBuildFromIntegrationTests.IsPresent)
{
    # For a regular test run, the binaries for the PowerShell worker get replaced after downloading and installing the Core Tools.
    Write-Host "Copying azure-functions-powershell-worker to Functions Host workers directory..."

    $configuration = if ($env:CONFIGURATION) { $env:CONFIGURATION } else { 'Debug' }
    Remove-Item -Recurse -Force -Path "$FUNC_CLI_DIRECTORY/workers/powershell"
    Copy-Item -Recurse -Force "$PSScriptRoot/../../src/bin/$configuration/$TARGET_FRAMEWORK/publish/" "$FUNC_CLI_DIRECTORY/workers/powershell/$POWERSHELL_VERSION"
    Copy-Item -Recurse -Force "$PSScriptRoot/../../src/bin/$configuration/$TARGET_FRAMEWORK/publish/worker.config.json" "$FUNC_CLI_DIRECTORY/workers/powershell"
}

Write-Host "Starting Functions Host..."

$Env:TestTaskHubName = $taskHubName
$Env:FUNCTIONS_WORKER_RUNTIME = "powershell"
$Env:FUNCTIONS_WORKER_RUNTIME_VERSION = $POWERSHELL_VERSION
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
