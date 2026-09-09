# Durable Timer Chains
# DurableTimerClient → DurableTimerOrchestrator
# CurrentUtcDateTimeClient → CurrentUtcDateTimeOrchestrator

using namespace System.Net

function DurableTimerClient {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = "Anonymous", Methods = "GET, POST")]
        $Request,

        $TriggerMetadata,

        [DurableClient()]
        $starter
    )

    Write-Host "DurableTimerClient started"
    $ErrorActionPreference = 'Stop'

    $InstanceId = Start-DurableOrchestration -FunctionName 'DurableTimerOrchestrator'
    Write-Host "Started orchestration with ID = '$InstanceId'"

    $Response = New-DurableOrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
    Push-OutputBinding -Name Response -Value $Response

    Write-Host "DurableTimerClient completed"
}

function DurableTimerOrchestrator {
    [AzFunction()]
    param(
        [OrchestrationTrigger()]
        $Context
    )

    $ErrorActionPreference = 'Stop'
    Write-Host "DurableTimerOrchestrator: started."

    $tempFile = New-TemporaryFile
    $tempDir = $tempFile.Directory.FullName
    Remove-Item $tempFile
    $fileName = "$("{0:MM_dd_yyyy_hh_mm_ss}" -f $Context.CurrentUtcDateTime)_timer_test.txt"
    $path = Join-Path -Path $tempDir -ChildPath $fileName

    Add-Content -Value "---" -Path $path
    Add-Content -Value $Context.CurrentUtcDateTime -Path $path

    Start-DurableTimer -Duration (New-TimeSpan -Seconds 5)

    Add-Content -Value $Context.CurrentUtcDateTime -Path $path

    Write-Host "DurableTimerOrchestrator: finished."
    return (Get-Content $path) -join "`n"
}

function CurrentUtcDateTimeClient {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = "Anonymous", Methods = "GET, POST")]
        $Request,

        $TriggerMetadata,

        [DurableClient()]
        $starter
    )

    Write-Host "CurrentUtcDateTimeClient started"
    $ErrorActionPreference = 'Stop'

    $InstanceId = Start-DurableOrchestration -FunctionName 'CurrentUtcDateTimeOrchestrator' -InputObject 'Hello'
    Write-Host "Started orchestration with ID = '$InstanceId'"

    $Response = New-DurableOrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
    Push-OutputBinding -Name Response -Value $Response

    Write-Host "CurrentUtcDateTimeClient completed"
}

function CurrentUtcDateTimeOrchestrator {
    [AzFunction()]
    param(
        [OrchestrationTrigger()]
        $Context
    )

    $ErrorActionPreference = 'Stop'
    Write-Host "CurrentUtcDateTimeOrchestrator started. Input: $($Context.Input)"

    $activityResults = @()

    $tempFile = New-TemporaryFile
    $tempDir = $tempFile.Directory.FullName
    Remove-Item $tempFile
    $fileName = "$("{0:MM_dd_yyyy_hh_mm_ss}" -f $Context.CurrentUtcDateTime)_datetime_test.txt"
    $path = Join-Path -Path $tempDir -ChildPath $fileName

    Add-Content -Value '---' -Path $path
    Add-Content -Value $Context.CurrentUtcDateTime -Path $path
    Add-Content -Value $Context.CurrentUtcDateTime -Path $path

    $activityResults += Invoke-DurableActivity -FunctionName "DurableActivity" -Input "Tokyo"
    Add-Content -Value $Context.CurrentUtcDateTime -Path $path

    Write-Host "About to start asynchronous calls."

    $tasks = @()
    $tasks += Invoke-DurableActivity -FunctionName "DurableActivity" -Input "Seattle" -NoWait
    Add-Content -Value $Context.CurrentUtcDateTime -Path $path

    $tasks += Invoke-DurableActivity -FunctionName "DurableActivity" -Input "London" -NoWait
    Add-Content -Value $Context.CurrentUtcDateTime -Path $path

    Write-Host "Finished the asynchronous calls."

    $activityResults += Wait-DurableTask -Task $tasks
    Add-Content -Value $Context.CurrentUtcDateTime -Path $path

    Write-Host "CurrentUtcDateTimeOrchestrator: finished."
    return (Get-Content $path) -join "`n"
}
