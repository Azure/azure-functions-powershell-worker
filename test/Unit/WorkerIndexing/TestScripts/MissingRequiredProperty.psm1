function MissingSchedule {
    [AzFunction()]
    param(
        [TimerTrigger()]
        $Timer
    )
    Write-Host "This should fail - TimerTrigger requires Schedule"
}
