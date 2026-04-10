function TimerJob {
    [AzFunction(Name = "MyTimerFunc")]
    param(
        [TimerTrigger(Schedule = "0 */5 * * * *")]
        $Timer
    )
    Write-Host "Timer triggered"
}
