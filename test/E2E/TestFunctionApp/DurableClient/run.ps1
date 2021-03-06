using namespace System.Net

param($Request, $TriggerMetadata)

Write-Host "DurableClient started"

$ErrorActionPreference = 'Stop'

$FunctionName = $Request.Query.FunctionName ?? 'DurableOrchestrator'

$InstanceId = Start-NewOrchestration -FunctionName $FunctionName -InputObject 'Hello'
Write-Host "Started orchestration with ID = '$InstanceId'"

$Response = New-OrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
Push-OutputBinding -Name Response -Value $Response

Write-Host "DurableClient completed"
