# Durable Event Handling Orchestrations
# DurableOrchestratorRaiseEvent, DurableOrchestratorComplexRaiseEvent, DurableOrchestratorGetTaskResult
# (All started via generic DurableClient with ?FunctionName=<name>)

function DurableOrchestratorRaiseEvent {
    [AzFunction()]
    param(
        [OrchestrationTrigger()]
        $Context
    )

    $output = @()
    $output += Start-DurableExternalEventListener -EventName "TESTEVENTNAME"
    $output
}

function DurableOrchestratorComplexRaiseEvent {
    [AzFunction()]
    param(
        [OrchestrationTrigger()]
        $Context
    )

    $output = @()
    Invoke-DurableActivity -FunctionName "DurableActivity" -Input "Tokyo"
    Invoke-DurableActivity -FunctionName "DurableActivity" -Input "Seattle"
    $output += Start-DurableExternalEventListener -EventName "TESTEVENTNAME"
    Invoke-DurableActivity -FunctionName "DurableActivity" -Input "London"
    $output
}

function DurableOrchestratorGetTaskResult {
    [AzFunction()]
    param(
        [OrchestrationTrigger()]
        $Context
    )

    $output = @()
    $task = Invoke-DurableActivity -FunctionName 'DurableActivity' -Input "world" -NoWait
    $firstTask = Wait-DurableTask -Task $task -Any
    $output += Get-DurableTaskResult -Task $firstTask
    $output
}
