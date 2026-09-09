function DuplicateBindingFunc {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = "Anonymous")]
        [QueueOutput(QueueName = "myqueue")]
        $Request
    )
    Write-Host "Two binding attributes on one parameter = duplicate binding name"
}
