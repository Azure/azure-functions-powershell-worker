# Durable Exception Handling Chain
# DurableOrchestratorWithException → DurableActivityWithException
# (Started via generic DurableClient with ?FunctionName=DurableOrchestratorWithException)

using namespace System.Net

function DurableOrchestratorWithException {
    [AzFunction()]
    param(
        [OrchestrationTrigger()]
        $Context
    )

    $ErrorActionPreference = 'Stop'
    Invoke-DurableActivity -FunctionName 'DurableActivityWithException' -Input 'Name' -ErrorAction Stop
    'This should not be returned'
}

function DurableActivityWithException {
    [AzFunction()]
    param(
        [ActivityTrigger()]
        $name
    )

    throw "Intentional exception ($name)"
}
