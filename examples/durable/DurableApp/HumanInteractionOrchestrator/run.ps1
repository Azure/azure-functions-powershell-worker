using namespace System.Net

param($Context)

Write-Host 'HumanInteractionOrchestrator: started.'

$output = @()

$duration = New-TimeSpan -Seconds $Context.Input.Duration
$managerId = $Context.Input.ManagerId

Invoke-ActivityFunction -FunctionName "RequestApproval" -Input $managerId

$durableTimeoutEvent = Start-DurableTimer -Duration $duration -NoWait
$approvalEvent = Start-DurableExternalEventListener -EventName "ApprovalEvent" -NoWait

$firstEvent = Wait-DurableTask -Task @($approvalEvent, $durableTimeoutEvent) -Any

if ($approvalEvent -eq $firstEvent) {
    Stop-DurableTimerTask -TimerTask $durableTimeout
    $output += Invoke-ActivityFunction -FunctionName "ProcessApproval" -Input $approvalEvent
}
else {
    $output += Invoke-ActivityFunction -FunctionName "EscalateApproval"
}

Write-Host 'HumanInteractionOrchestrator: finished.'

$output
