using namespace System.Net

param($Request, $TriggerMetadata)

Write-Host "CurrentUtcDateTimeStart started"

$ErrorActionPreference = 'Stop'

$InstanceId = Start-NewOrchestration -FunctionName 'CurrentUtcDateTimeOrchestrator' -InputObject 'Hello'
Write-Host "Started orchestration with ID = '$InstanceId'"

$Response = New-OrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
Push-OutputBinding -Name Response -Value $Response

Write-Host "CurrentUtcDateTimeStart completed"
