function ProcessQueue {
    [AzFunction()]
    param(
        [QueueTrigger(QueueName = "myqueue", Connection = "StorageConn")]
        $QueueItem,

        [QueueOutput(QueueName = "outqueue", Connection = "StorageConn")]
        $OutputQueue
    )
    Push-OutputBinding -Name OutputQueue -Value $QueueItem
}
