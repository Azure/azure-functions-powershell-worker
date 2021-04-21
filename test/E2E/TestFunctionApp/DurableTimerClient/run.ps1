using namespace System.Net

param($Request, $TriggerMetadata)

Write-Host "DurableTimerClient started"

$ErrorActionPreference = 'Stop'

$InstanceId = Start-NewOrchestration -FunctionName 'DurableTimerOrchestrator'
Write-Host "Started orchestration with ID = '$InstanceId'"

$Response = New-DurableOrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
Push-OutputBinding -Name Response -Value $Response

Write-Host "DurableTimerClient completed"
