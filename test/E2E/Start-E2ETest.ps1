#	
# Copyright (c) Microsoft. All rights reserved.	
# Licensed under the MIT license. See LICENSE file in the project root for full license information.	
#
param
(
    [Switch] $UseCoreToolsBuildFromIntegrationTests,
    [Switch] $UseEmulators,
    [string] $CoreToolsPath,
    [string] $TestFilter
)

$originalPSModulePath = $env:PSModulePath
$primaryError = $null

try
{
if ($UseEmulators.IsPresent)
{
    . "$PSScriptRoot/Start-E2EEmulators.ps1"
}

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

$FUNC_RUNTIME_VERSION = '4'
$TARGET_FRAMEWORK = 'net10.0'
$POWERSHELL_VERSION = '7.6'

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

$FUNC_CLI_DIRECTORY = Join-Path $PSScriptRoot 'Azure.Functions.Cli'
$workersDirectory = Join-Path $PSScriptRoot 'Azure.Functions.Workers'

if ($CoreToolsPath)
{
    $resolvedCoreToolsPath = Resolve-Path $CoreToolsPath
    $funcExePath = if (Test-Path $resolvedCoreToolsPath -PathType Container)
    {
        Join-Path $resolvedCoreToolsPath $FUNC_EXE_NAME
    }
    else
    {
        $resolvedCoreToolsPath.Path
    }

    if (-not (Test-Path $funcExePath -PathType Leaf))
    {
        throw "Functions Core Tools was not found at '$funcExePath'."
    }

    Write-Host "Using Functions Core Tools from '$funcExePath'."
}
else
{
    if ($UseCoreToolsBuildFromIntegrationTests.IsPresent)
    {
        $versionUrl = "https://functionsintegclibuilds.blob.core.windows.net/builds/$FUNC_RUNTIME_VERSION/latest/version.txt"
        $coreToolsDownloadURL = "https://functionsintegclibuilds.blob.core.windows.net/builds/$FUNC_RUNTIME_VERSION/latest/Azure.Functions.Cli.$os-$arch.zip"
        $version = Invoke-RestMethod -Uri $versionUrl
    }
    else
    {
        $releaseApiUrl = 'https://api.github.com/repos/Azure/azure-functions-core-tools/releases/latest'
        $release = Invoke-RestMethod -Uri $releaseApiUrl -Headers @{ 'User-Agent' = 'azure-functions-powershell-worker' }
        $assetNamePrefix = "Azure.Functions.Cli.$os-$arch."
        $asset = $release.assets |
            Where-Object { $_.name.StartsWith($assetNamePrefix) -and $_.name.EndsWith('.zip') } |
            Select-Object -First 1

        if (-not $asset)
        {
            throw "Could not find a Functions Core Tools asset matching '$assetNamePrefix*.zip' in release $($release.tag_name)."
        }

        $version = $release.tag_name
        $coreToolsDownloadURL = $asset.browser_download_url
    }

    Write-Host 'Deleting Functions Core Tools if exists...'
    Remove-Item -Force "$FUNC_CLI_DIRECTORY.zip" -ErrorAction Ignore
    Remove-Item -Recurse -Force $FUNC_CLI_DIRECTORY -ErrorAction Ignore

    Write-Host "Downloading Functions Core Tools (Version: $version)..."

    $output = "$FUNC_CLI_DIRECTORY.zip"
    Invoke-RestMethod -Uri $coreToolsDownloadURL -OutFile $output

    Write-Host 'Extracting Functions Core Tools...'
    Expand-Archive $output -DestinationPath $FUNC_CLI_DIRECTORY
    Remove-Item -Force $output

    $funcExePath = Join-Path $FUNC_CLI_DIRECTORY $FUNC_EXE_NAME
}

if (-not $UseCoreToolsBuildFromIntegrationTests.IsPresent)
{
    Write-Host "Preparing the locally built PowerShell worker..."
    $configuration = if ($env:CONFIGURATION) { $env:CONFIGURATION } else { 'Debug' }
    $workerPublishDirectory = "$PSScriptRoot/../../src/bin/$configuration/$TARGET_FRAMEWORK/publish"

    if (-not (Test-Path "$workerPublishDirectory/worker.config.json"))
    {
        throw "The PowerShell worker was not found at '$workerPublishDirectory'. Build it before running E2E tests."
    }

    Remove-Item -Recurse -Force $workersDirectory -ErrorAction Ignore
    New-Item -ItemType Directory -Path "$workersDirectory/powershell/$POWERSHELL_VERSION" -Force | Out-Null
    Copy-Item -Recurse -Force "$workerPublishDirectory/*" "$workersDirectory/powershell/$POWERSHELL_VERSION"
    Copy-Item -Force "$workerPublishDirectory/worker.config.json" "$workersDirectory/powershell"
    $env:languageWorkers__workersDirectory = $workersDirectory
    $env:PSModulePath = (($originalPSModulePath -split [System.IO.Path]::PathSeparator |
        Where-Object { -not $_.StartsWith($HOME, [System.StringComparison]::OrdinalIgnoreCase) }) -join
        [System.IO.Path]::PathSeparator)
}

Write-Host "Starting Functions Host..."

$Env:TestTaskHubName = $taskHubName
$Env:FUNCTIONS_WORKER_RUNTIME = "powershell"
$Env:FUNCTIONS_WORKER_RUNTIME_VERSION = $POWERSHELL_VERSION
$Env:AZURE_FUNCTIONS_ENVIRONMENT = "development"
$Env:FUNCTIONS_CORE_TOOLS_EXE = $funcExePath

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

$testArguments = @(
    'test',
    "$PSScriptRoot/Azure.Functions.PowerShellWorker.E2E/Azure.Functions.PowerShellWorker.E2E/Azure.Functions.PowerShellWorker.E2E.csproj",
    '--logger:trx',
    '--results-directory',
    "$PSScriptRoot/../../testResults"
)
if ($TestFilter)
{
    $testArguments += @('--filter', $TestFilter)
}

& dotnet @testArguments
if ($LASTEXITCODE -ne 0) { throw "xunit tests failed." }

Write-Host "-----------------------------------------------------------------------------" -ForegroundColor Green
}
catch
{
    $primaryError = $_
}
finally
{
    $cleanupErrors = [System.Collections.Generic.List[string]]::new()

    if ($UseEmulators.IsPresent)
    {
        try
        {
            & "$PSScriptRoot/Stop-E2EEmulators.ps1"
        }
        catch
        {
            $cleanupErrors.Add($_.Exception.Message)
        }
    }

    if ($workersDirectory)
    {
        try
        {
            Remove-Item -Recurse -Force $workersDirectory -ErrorAction Stop
        }
        catch
        {
            $cleanupErrors.Add($_.Exception.Message)
        }
    }

    $env:PSModulePath = $originalPSModulePath
}

if ($primaryError)
{
    if ($cleanupErrors.Count -gt 0)
    {
        Write-Warning "Cleanup also failed:`n- $($cleanupErrors -join "`n- ")"
    }

    throw $primaryError
}

if ($cleanupErrors.Count -gt 0)
{
    throw "E2E cleanup failed:`n- $($cleanupErrors -join "`n- ")"
}
