using namespace System.Net

param($Request, $TriggerMetadata)

Write-Host "HttpTrigger started"

$InstanceId = Start-NewOrchestration -FunctionName 'MyOrchestrator' -InputObject 'Hello'
Write-Host "Started orchestration with ID = '$InstanceId'"

$Response = New-OrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
Push-OutputBinding -Name Response -Value $Response

Write-Host "HttpTrigger completed"
