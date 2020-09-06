using namespace System.Net

param($Request, $TriggerMetadata)

Write-Host 'HumanInteractionStart started'

$OrchestratorInputs = @{ Duration = 45; ManagerId = 1; SkipManagerId = 2 }

$InstanceId = Start-NewOrchestration -FunctionName 'HumanInteractionOrchestrator' -InputObject $OrchestratorInputs
Write-Host "Started orchestration with ID = '$InstanceId'"

$Response = New-OrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
Push-OutputBinding -Name Response -Value $Response

Write-Host 'HumanInteractionStart completed'
