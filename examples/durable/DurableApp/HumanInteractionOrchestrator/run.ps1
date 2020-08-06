using namespace System.Net

param($Context)

Write-Host 'HumanInteractionOrchestrator: started.'

$output = @()

$duration = $Context.Input.Duration

Invoke-ActivityFunction -FunctionName "RequestApproval"

$durableTimeoutEvent = Start-DurableTimer -Duration $duration -NoWait
<<<<<<< HEAD
<<<<<<< HEAD
$approvalEvent = Start-DurableExternalEventListener -EventName "ApprovalEvent" -NoWait
=======
$approvalEvent = Start-EventListener -EventName "ApprovalEvent" -NoWait
>>>>>>> 3deb55c... Modified name of external event cmdlet and added NoWait flag
=======
$approvalEvent = Start-DurableExternalEventListener -EventName "ApprovalEvent" -NoWait
>>>>>>> 9bbe9b4... Modified new cmdlet names

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
