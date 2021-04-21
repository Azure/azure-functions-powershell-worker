using namespace System.Net

param($Request, $TriggerMetadata)

Write-Host "CurrentUtcDateTimeClient started"

$ErrorActionPreference = 'Stop'

$InstanceId = Start-DurableOrchestration -FunctionName 'CurrentUtcDateTimeOrchestrator' -InputObject 'Hello'
Write-Host "Started orchestration with ID = '$InstanceId'"

$Response = New-DurableOrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
Push-OutputBinding -Name Response -Value $Response

Write-Host "CurrentUtcDateTimeClient completed"
