# Durable External Event Chain
# ExternalEventClient → ExternalEventOrchestrator

using namespace System.Net

function ExternalEventClient {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = "Anonymous", Methods = "GET, POST")]
        $Request,

        $TriggerMetadata,

        [DurableClient()]
        $starter
    )

    Write-Host "ExternalEventClient started"
    $ErrorActionPreference = 'Stop'

    $OrchestratorInputs = @{ FirstDuration = 5; SecondDuration = 60 }

    $InstanceId = Start-DurableOrchestration -FunctionName 'ExternalEventOrchestrator' -InputObject $OrchestratorInputs
    Write-Host "Started orchestration with ID = '$InstanceId'"

    $Response = New-DurableOrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
    Push-OutputBinding -Name Response -Value $Response

    Start-Sleep -Seconds 3
    Send-DurableExternalEvent -InstanceId $InstanceId -EventName "SecondExternalEvent"

    Write-Host "ExternalEventClient completed"
}

function ExternalEventOrchestrator {
    [AzFunction()]
    param(
        [OrchestrationTrigger()]
        $Context
    )

    $ErrorActionPreference = 'Stop'
    Write-Host "ExternalEventOrchestrator started."

    $output = @()

    $firstDuration = New-TimeSpan -Seconds $Context.Input.FirstDuration
    $secondDuration = New-TimeSpan -Seconds $Context.Input.SecondDuration

    $firstTimeout = Start-DurableTimer -Duration $firstDuration -NoWait
    $firstExternalEvent = Start-DurableExternalEventListener -EventName "FirstExternalEvent" -NoWait
    $firstCompleted = Wait-DurableTask -Task $firstTimeout, $firstExternalEvent -Any

    if ($firstCompleted -eq $firstTimeout) {
        $output += "FirstTimeout"
    }
    else {
        $output += "FirstExternalEvent"
        Stop-DurableTimerTask -Task $firstTimeout
    }

    $secondTimeout = Start-DurableTimer -Duration $secondDuration -NoWait
    $secondExternalEvent = Start-DurableExternalEventListener -EventName "SecondExternalEvent" -NoWait
    $secondCompleted = Wait-DurableTask -Task $secondTimeout, $secondExternalEvent -Any

    if ($secondCompleted -eq $secondTimeout) {
        $output += "SecondTimeout"
    }
    else {
        $output += "SecondExternalEvent"
        Stop-DurableTimerTask -Task $secondTimeout
    }

    return $output
}
