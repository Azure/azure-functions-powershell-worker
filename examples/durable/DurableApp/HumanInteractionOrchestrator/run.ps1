using namespace System.Net

param($Context)

Write-Host 'HumanInteractionOrchestrator: started.'

$output = @()

$duration = $Context.Input.Duration

$approvalEvent = Invoke-ActivityFunction -FunctionName "RequestApproval" -NoWait
$durableTimeoutEvent = Start-DurableTimer -Duration $duration -NoWait

$firstEvent = Wait-AnyEvent -Event @($approvalEvent, $durableTimeoutEvent)

if ($approvalEvent -eq $firstEvent) {
    $durableTimeout.Cancel()
    $output += Invoke-ActivityFunction -FunctionName "ProcessApproval" -Input $approvalEvent
}
else {
    $output += Invoke-ActivityFunction -FunctionName "EscalateApproval"
}

Write-Host 'HumanInteractionOrchestrator: finished.'

$output
