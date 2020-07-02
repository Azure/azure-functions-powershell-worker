param($Request, $TriggerMetadata)

Write-Host 'MonitorStart started'

$OrchestratorInputs = @{ JobId = $null; PollingInterval = 10; ExpiryTime = (Get-Date).addSeconds(60).ToUniversalTime() }

$InstanceId = Start-NewOrchestration -FunctionName 'MonitorOrchestrator' -InputObject $OrchestratorInputs
Write-Host "Started orchestration with ID = '$InstanceId'"

$Response = New-OrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
Push-OutputBinding -Name Response -Value $Response

Write-Host 'MonitorStart completed'
