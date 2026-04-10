function NoTriggerFunc {
    [AzFunction()]
    param(
        [QueueOutput(QueueName = "myqueue")]
        $OutputQueue
    )
    Write-Host "This should fail validation - no trigger"
}
