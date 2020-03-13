using namespace System.Net

param($Request, $TriggerMetadata)

Write-Host 'FunctionChainingStart started'

$InstanceId = Start-NewOrchestration -FunctionName 'FunctionChainingOrchestrator' -InputObject 'Hello'
Write-Host "Started orchestration with ID = '$InstanceId'"

$Response = New-OrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
Push-OutputBinding -Name Response -Value $Response

Write-Host 'FunctionChainingStart completed'
