using namespace System.Net

param($Request, $TriggerMetadata)

Write-Host 'MonitorStart started'

$OrchestratorInputs = @{ JobId = 1; MachineId = 1; PollingInterval = 10; ExpiryTime = (Get-Date).ToUniversalTime().AddSeconds(60) }

$InstanceId = Start-NewOrchestration -FunctionName 'MonitorOrchestrator' -InputObject $OrchestratorInputs
Write-Host "Started orchestration with ID = '$InstanceId'"

$Response = New-OrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
Push-OutputBinding -Name Response -Value $Response

Write-Host 'MonitorStart completed'
