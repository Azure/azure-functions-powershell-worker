function BadFunction {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = "Anonymous")]
        $Request,

        [TimerTrigger(Schedule = "0 */5 * * * *")]
        $Timer
    )
    Write-Host "This should fail validation"
}
