# Durable Queue Output Chain
# DurableOrchestratorWriteToQueue → DurableActivityWritesToQueue
# (Started via generic DurableClient with ?FunctionName=DurableOrchestratorWriteToQueue)

using namespace System.Net

function DurableOrchestratorWriteToQueue {
    [AzFunction()]
    param(
        [OrchestrationTrigger()]
        $Context
    )

    Invoke-DurableActivity -FunctionName 'DurableActivityWritesToQueue' -Input 'QueueData'
}

function DurableActivityWritesToQueue {
    [AzFunction()]
    param(
        [ActivityTrigger()]
        $name,

        [QueueOutput(QueueName = "outqueue", Connection = "AzureWebJobsStorage")]
        $outputQueueItem
    )

    Write-Information "Pushing to outputQueueItem output binding"
    Push-OutputBinding -Name outputQueueItem -Value $name
    Write-Information "Done"

    "Hello $name!"
}
