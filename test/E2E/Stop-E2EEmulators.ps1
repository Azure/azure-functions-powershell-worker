# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

$ErrorActionPreference = 'Stop'

$composeFile = Join-Path $PSScriptRoot 'Emulators' 'eventhubs' 'docker-compose.yml'
$cosmosContainerName = 'powershell-worker-cosmos-emulator'
$cleanupErrors = [System.Collections.Generic.List[string]]::new()

try
{
    & docker container inspect $cosmosContainerName *> $null
    if ($LASTEXITCODE -eq 0)
    {
        & docker container rm --force $cosmosContainerName
        if ($LASTEXITCODE -ne 0)
        {
            throw "Failed to remove the $cosmosContainerName container."
        }
    }
}
catch
{
    $cleanupErrors.Add($_.Exception.Message)
}

try
{
    & docker compose --file $composeFile down --volumes --remove-orphans
    if ($LASTEXITCODE -ne 0)
    {
        throw 'Failed to stop the Event Hubs emulator and Azurite containers.'
    }
}
catch
{
    $cleanupErrors.Add($_.Exception.Message)
}

if ($cleanupErrors.Count -gt 0)
{
    throw "Emulator cleanup failed:`n- $($cleanupErrors -join "`n- ")"
}
