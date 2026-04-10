# Durable Legacy Names Chain
# DurableClientLegacyNames → DurableOrchestratorLegacyNames → (reuses DurableActivity)

using namespace System.Net

function DurableClientLegacyNames {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = "Anonymous", Methods = "GET, POST")]
        $Request,

        $TriggerMetadata,

        [DurableClient()]
        $starter
    )

    Write-Host "DurableClientLegacyNames started"
    $ErrorActionPreference = 'Stop'

    $InstanceId = Start-NewOrchestration -FunctionName 'DurableOrchestratorLegacyNames' -InputObject 'Hello'
    Write-Host "Started orchestration with ID = '$InstanceId'"

    $Response = New-OrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
    Push-OutputBinding -Name Response -Value $Response

    Write-Host "DurableClientLegacyNames completed"
}

function DurableOrchestratorLegacyNames {
    [AzFunction()]
    param(
        [OrchestrationTrigger()]
        $Context
    )

    $ErrorActionPreference = 'Stop'
    Write-Host "DurableOrchestratorLegacyNames: started. Input: $($Context.Input)"

    Invoke-ActivityFunction -FunctionName "DurableActivity" -Input "Tokyo"

    Write-Host "DurableOrchestratorLegacyNames: finished."

    return $output
}
