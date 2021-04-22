using namespace System.Net

param($Context)

Write-Host 'HumanInteractionOrchestrator: started.'

$output = @()

$duration = New-TimeSpan -Seconds $Context.Input.Duration
$managerId = $Context.Input.ManagerId
$skipManagerId = $Context.Input.SkipManagerId

$output += Invoke-DurableActivity -FunctionName "RequestApproval" -Input $managerId

$durableTimeoutEvent = Start-DurableTimer -Duration $duration -NoWait
$approvalEvent = Start-DurableExternalEventListener -EventName "ApprovalEvent" -NoWait

$firstEvent = Wait-DurableTask -Task @($approvalEvent, $durableTimeoutEvent) -Any

if ($approvalEvent -eq $firstEvent) {
    Stop-DurableTimerTask -Task $durableTimeoutEvent
    $output += Invoke-DurableActivity -FunctionName "ProcessApproval" -Input $approvalEvent
}
else {
    $output += Invoke-DurableActivity -FunctionName "EscalateApproval" -Input $skipManagerId
}

Write-Host 'HumanInteractionOrchestrator: finished.'

$output
