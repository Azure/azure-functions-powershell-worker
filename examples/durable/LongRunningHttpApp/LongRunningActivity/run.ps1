param($InputData)

$name = $InputData.Name
$durationSeconds = $InputData.DurationSeconds

$activityTraceHeader = "LongRunningActivity($name, $durationSeconds)"
Write-Host "$activityTraceHeader started"

$startedAt = Get-Date
$runUntil = (Get-Date) + (New-TimeSpan -Seconds $durationSeconds)
Write-Host "$activityTraceHeader will run until $($runUntil.ToUniversalTime())"
while ((Get-Date) -lt $runUntil) {
    Start-Sleep -Seconds ([Math]::Min($durationSeconds, 10))
    Write-Host "$activityTraceHeader is still running: $([Math]::Truncate(((Get-Date) - $startedAt).TotalMinutes)) minute(s)"
}

Write-Host "$activityTraceHeader finished"

return "Hello $name"
