using namespace System.Net

param($Context)

Write-Host 'HumanInteractionOrchestrator: started.'

$output = @()

$duration = $Context.Input.Duration

Invoke-ActivityFunction -FunctionName "RequestApproval"

$durableTimeoutEvent = Start-DurableTimer -Duration $duration -NoWait
$approvalEvent = Start-EventListener -EventName "ApprovalEvent" -NoWait

$firstEvent = Wait-AnyTask -Task @($approvalEvent, $durableTimeoutEvent)

if ($approvalEvent -eq $firstEvent) {
    $durableTimeout.Cancel()
    $output += Invoke-ActivityFunction -FunctionName "ProcessApproval" -Input $approvalEvent
}
else {
    $output += Invoke-ActivityFunction -FunctionName "EscalateApproval"
}

Write-Host 'HumanInteractionOrchestrator: finished.'

$output
