param($Request, $TriggerMetadata)

Write-Host "DurableClient started"

$InstanceId = Start-NewOrchestration -FunctionName 'DurableOrchestrator' -InputObject 'Hello'
Write-Host "Started orchestration with ID = '$InstanceId'"

$Response = New-OrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
Push-OutputBinding -Name Response -Value $Response

Write-Host "DurableClient completed"
