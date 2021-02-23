using namespace System.Net

param($Request, $TriggerMetadata)

Write-Host 'FunctionChainingWithRetriesStart started'

$InstanceId = Start-NewOrchestration -FunctionName 'FunctionChainingWithRetriesOrchestrator' -InputObject 'Hello'
Write-Host "Started orchestration with ID = '$InstanceId'"

$Response = New-OrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
Push-OutputBinding -Name Response -Value $Response

Write-Host 'FunctionChainingWithRetriesStart completed'
