using namespace System.Net

param($Request, $TriggerMetadata)

Write-Host 'MonitorStart started'

$OrchestratorInputs = @{ JobId = $null; PollingInterval = 30; ExpiryTime = (Get-Time).addSeconds(60)}

$InstanceId = Start-NewOrchestration -FunctionName 'MonitorOrchestrator' -InputObject $OrchestratorInputs
Write-Host "Started orchestration with ID = '$InstanceId'"

$Response = New-OrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
Push-OutputBinding -Name Response -Value $Response

Write-Host 'MonitorStart completed'
