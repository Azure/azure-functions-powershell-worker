# Queue Trigger with Queue Output using the V2 attribute-based programming model.

function ProcessQueueMessage {
    [AzFunction()]
    param(
        [QueueTrigger(QueueName = 'input-queue', Connection = 'AzureWebJobsStorage')]
        $QueueItem,

        [QueueOutput(QueueName = 'output-queue', Connection = 'AzureWebJobsStorage')]
        $OutputQueue
    )

    Write-Information "Processing queue message: $QueueItem"

    # Transform the message and send to output queue
    $result = @{
        OriginalMessage = $QueueItem
        ProcessedAt     = (Get-Date).ToString('o')
        Status          = 'Processed'
    } | ConvertTo-Json

    Push-OutputBinding -Name OutputQueue -Value $result
}
