# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

$ErrorActionPreference = 'Stop'

$composeFile = Join-Path $PSScriptRoot 'Emulators' 'eventhubs' 'docker-compose.yml'
$cosmosContainerName = 'powershell-worker-cosmos-emulator'
$cosmosImage = 'mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview'
$cosmosKey = 'C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=='

function Invoke-Docker
{
    param([Parameter(Mandatory)][string[]] $Arguments)

    & docker @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "docker $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Wait-ForCosmosEmulator
{
    $timeout = [TimeSpan]::FromMinutes(3)
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    while ($stopwatch.Elapsed -lt $timeout)
    {
        try
        {
            $response = Invoke-WebRequest -Uri 'http://localhost:8080/ready' -TimeoutSec 5
            if ($response.StatusCode -eq 200)
            {
                return
            }
        }
        catch
        {
            Start-Sleep -Seconds 2
        }
    }

    Invoke-Docker -Arguments @('logs', $cosmosContainerName)
    throw 'The Cosmos DB emulator did not become ready within three minutes.'
}

function Wait-ForEventHubsEmulator
{
    $timeout = [TimeSpan]::FromMinutes(3)
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    while ($stopwatch.Elapsed -lt $timeout)
    {
        $logs = & docker logs powershell-worker-eventhubs-emulator 2>&1
        if ($LASTEXITCODE -eq 0 -and $logs -match 'Emulator Service is Successfully Up')
        {
            return
        }

        Start-Sleep -Seconds 2
    }

    Invoke-Docker -Arguments @('logs', 'powershell-worker-eventhubs-emulator')
    throw 'The Event Hubs emulator did not become ready within three minutes.'
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue))
{
    throw 'Docker is required to run the emulator-backed E2E tests.'
}

Invoke-Docker -Arguments @('compose', 'version')

& "$PSScriptRoot/Stop-E2EEmulators.ps1"

Invoke-Docker -Arguments @('compose', '--file', $composeFile, 'up', '--detach', '--pull', 'always')
Invoke-Docker -Arguments @('pull', $cosmosImage)
Invoke-Docker -Arguments @(
    'run',
    '--detach',
    '--name', $cosmosContainerName,
    '--env', 'ENABLE_EXPLORER=false',
    '--publish', '8080:8080',
    '--publish', '8081:8081',
    $cosmosImage
)

Wait-ForEventHubsEmulator
Wait-ForCosmosEmulator

$env:AzureWebJobsStorage = 'UseDevelopmentStorage=true'
$env:AzureWebJobsCosmosDBConnectionString = "AccountEndpoint=http://localhost:8081/;AccountKey=$cosmosKey"
$env:AzureWebJobsEventHubSender = 'Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;'
