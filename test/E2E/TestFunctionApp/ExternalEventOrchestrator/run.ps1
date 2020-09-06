using namespace System.Net

param($Context)

$ErrorActionPreference = 'Stop'

Write-Host "ExternalEventOrchestrator started."

$output = @()

$firstDuration = New-TimeSpan -Seconds $Context.Input.FirstDuration
$secondDuration = New-TimeSpan -Seconds $Context.Input.SecondDuration

$firstTimeout = Start-DurableTimer -Duration $firstDuration -NoWait
$firstExternalEvent = Start-DurableExternalEventListener -EventName "FirstExternalEvent" -NoWait
$firstCompleted = Wait-DurableTask -Task @($firstTimeout, $firstExternalEvent) -Any

if ($firstCompleted -eq $firstTimeout) {
    $output += "FirstTimeout"
}
else {
    $output += "FirstExternalEvent"
    Stop-DurableTimerTask -Task $firstTimeout
}

$secondTimeout = Start-DurableTimer -Duration $secondDuration -NoWait
$secondExternalEvent = Start-DurableExternalEventListener -EventName "SecondExternalEvent" -NoWait
$secondCompleted = Wait-DurableTask -Task @($secondTimeout, $secondExternalEvent) -Any

if ($secondCompleted -eq $secondTimeout) {
    $output += "SecondTimeout"
}
else {
    $output += "SecondExternalEvent"
    # If Stop-DurableTimerTask does not work as intended, then the orchestration will time out
    Stop-DurableTimerTask -Task $secondTimeout
}

return $output
