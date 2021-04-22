using namespace System.Net

param($Request, $TriggerMetadata)

Write-Host "ExternalEventClient started"

$ErrorActionPreference = 'Stop'

$OrchestratorInputs = @{ FirstDuration = 5; SecondDuration = 60 }

$InstanceId = Start-DurableOrchestration -FunctionName 'ExternalEventOrchestrator' -InputObject $OrchestratorInputs
Write-Host "Started orchestration with ID = '$InstanceId'"

$Response = New-DurableOrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
Push-OutputBinding -Name Response -Value $Response

Start-Sleep -Seconds 15
Send-DurableExternalEvent -InstanceId $InstanceId -EventName "SecondExternalEvent"

Write-Host "ExternalEventClient completed"
