# Storage Functions
# Groups: QueueTriggerAndOutput

function QueueTriggerAndOutput {
    [AzFunction()]
    param(
        [QueueTrigger(QueueName = "test-input-ps", Connection = "AzureWebJobsStorage")]
        $myQueueItem,

        [QueueOutput(QueueName = "test-output-ps", Connection = "AzureWebJobsStorage")]
        $outQueueItem
    )

    Write-Host "PowerShell queue trigger function processed work item $myQueueItem"
    Push-OutputBinding -Name outQueueItem -Value $myQueueItem
}
