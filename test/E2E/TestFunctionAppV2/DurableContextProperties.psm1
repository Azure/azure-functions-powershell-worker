# Durable Context Properties Chain
# DurableClientOrchContextProperties → DurableOrchestratorAccessContextProps → (reuses DurableActivity)

using namespace System.Net

function DurableClientOrchContextProperties {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = "Function", Methods = "POST, GET")]
        $Request,

        $TriggerMetadata,

        [DurableClient()]
        $starter
    )

    $InstanceId = Start-DurableOrchestration -FunctionName "DurableOrchestratorAccessContextProps" -InstanceId "myInstanceId"
    Write-Host "Started orchestration with ID = '$InstanceId'"

    $Response = New-DurableOrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
    Push-OutputBinding -Name Response -Value $Response
}

function DurableOrchestratorAccessContextProps {
    [AzFunction()]
    param(
        [OrchestrationTrigger()]
        $Context
    )

    $output = @()
    $output += $Context.IsReplaying
    $output += Invoke-DurableActivity -FunctionName 'DurableActivity' -Input $Context.InstanceId
    $output += $Context.IsReplaying
    $output
}
