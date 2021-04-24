using namespace System.Net

param($Request, $TriggerMetadata)

Write-Host "DurableClient started"

$ErrorActionPreference = 'Stop'

$FunctionName = $Request.Query.FunctionName ?? 'DurableOrchestrator'

$InstanceId = Start-DurableOrchestration -FunctionName $FunctionName -InputObject 'Hello'
Write-Host "Started orchestration with ID = '$InstanceId'"

$Response = New-DurableOrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
Push-OutputBinding -Name Response -Value $Response

$Status = Get-DurableStatus -InstanceId $InstanceId
Write-Host "Orchestration $InstanceId status: $($Status | ConvertTo-Json)"
if ($Status.runtime -ne 'Running') {
    throw "Unexpected orchestration $InstanceId runtime status: $($Status.runtime)"
}

Write-Host "DurableClient completed"
