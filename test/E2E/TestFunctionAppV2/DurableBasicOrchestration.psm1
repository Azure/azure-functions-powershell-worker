# Durable Basic Orchestration Chain
# DurableClient (generic HTTP starter) → DurableOrchestrator → DurableActivity, DurableActivityFlaky
# DurableClientTerminating also uses DurableOrchestrator

using namespace System.Net

function DurableClient {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = "Anonymous", Methods = "GET, POST")]
        $Request,

        $TriggerMetadata,

        [DurableClient()]
        $starter
    )

    Write-Host "DurableClient started"
    $ErrorActionPreference = 'Stop'

    $FunctionName = $Request.Query.FunctionName ?? 'DurableOrchestrator'

    $InstanceId = Start-DurableOrchestration -FunctionName $FunctionName -InputObject 'Hello'
    Write-Host "Started orchestration with ID = '$InstanceId'"

    $Response = New-DurableOrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
    Push-OutputBinding -Name Response -Value $Response

    $Status = Get-DurableStatus -InstanceId $InstanceId
    Write-Host "Orchestration $InstanceId status: $($Status | ConvertTo-Json)"
    if ($Status.runtimeStatus -notin 'Pending', 'Running', 'Failed', 'Completed') {
        throw "Unexpected orchestration $InstanceId runtime status: $($Status.runtimeStatus)"
    }

    Write-Host "DurableClient completed"
}

function DurableClientTerminating {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = "Anonymous", Methods = "GET, POST")]
        $Request,

        $TriggerMetadata,

        [DurableClient()]
        $starter
    )

    Write-Host "DurableClient started"
    $ErrorActionPreference = 'Stop'

    $FunctionName = $Request.Query.FunctionName ?? 'DurableOrchestrator'

    $InstanceId = Start-DurableOrchestration -FunctionName $FunctionName -InputObject 'Hello'
    Write-Host "Started orchestration with ID = '$InstanceId'"

    Stop-DurableOrchestration -InstanceId $InstanceId -Reason 'Terminated intentionally'

    $Response = New-DurableOrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
    Push-OutputBinding -Name Response -Value $Response

    Write-Host "DurableClient completed"
}

function DurableOrchestrator {
    [AzFunction()]
    param(
        [OrchestrationTrigger()]
        $Context
    )

    $ErrorActionPreference = 'Stop'
    Write-Host "DurableOrchestrator: started. Input: $($Context.Input)"

    Set-DurableCustomStatus -CustomStatus 'Custom status: started'

    # Function chaining
    $output = @()
    $output += Invoke-DurableActivity -FunctionName "DurableActivity" -Input "Tokyo"

    # Fan-out/Fan-in
    $tasks = @()
    $tasks += Invoke-DurableActivity -FunctionName "DurableActivity" -Input "Seattle" -NoWait
    $tasks += Invoke-DurableActivity -FunctionName "DurableActivity" -Input "London" -NoWait
    $output += Wait-DurableTask -Task $tasks

    # Retries
    $retryOptions = New-DurableRetryOptions -FirstRetryInterval (New-Timespan -Seconds 2) -MaxNumberOfAttempts 5
    $inputData = @{ Name = 'Toronto'; StartTime = $Context.CurrentUtcDateTime }
    $output += Invoke-DurableActivity -FunctionName "DurableActivityFlaky" -Input $inputData -RetryOptions $retryOptions

    Set-DurableCustomStatus -CustomStatus 'Custom status: finished'
    Write-Host "DurableOrchestrator: finished."

    return $output
}

function DurableActivity {
    [AzFunction()]
    param(
        [ActivityTrigger()]
        $name
    )

    Write-Host "DurableActivity($name) started"
    Start-Sleep -Seconds 1
    Write-Host "DurableActivity($name) finished"

    "Hello $name"
}

function DurableActivityFlaky {
    [AzFunction()]
    param(
        [ActivityTrigger()]
        $InputData
    )

    # Intentional intermittent error, eventually "self-healing"
    $elapsedTime = (Get-Date).ToUniversalTime() - $InputData.StartTime
    if ($elapsedTime.TotalSeconds -lt 3) {
        throw 'Nope, no luck this time...'
    }

    "Hello $($InputData.Name)"
}
