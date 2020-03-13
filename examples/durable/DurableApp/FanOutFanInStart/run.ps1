using namespace System.Net

param($Request, $TriggerMetadata)

Write-Host 'FanOutFanInStart started'

$InstanceId = Start-NewOrchestration -FunctionName 'FanOutFanInOrchestrator' -InputObject 'Hello'
Write-Host "Started orchestration with ID = '$InstanceId'"

$Response = New-OrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
Push-OutputBinding -Name Response -Value $Response

Write-Host 'FanOutFanInStart completed'
