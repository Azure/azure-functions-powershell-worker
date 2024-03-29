using namespace System.Net

param($Request, $TriggerMetadata)

Write-Host 'InstanceManagement started'

$InstanceId = Start-DurableOrchestration -FunctionName 'FunctionChainingOrchestrator' -InputObject $Request.Query.Input
Write-Host "Started orchestration $($Request.Params.FunctionName) with ID = '$InstanceId'"

do {
    $Status = Get-DurableStatus -InstanceId $InstanceId -ShowHistory -ShowHistoryOutput -ShowInput
    Write-Host "Status: $($Status | ConvertTo-Json)"
    Start-Sleep -Seconds 5
} while ($Status.runtimeStatus -ne 'Running')

Write-Host "Terminating orchestration $InstanceId..."
Stop-DurableOrchestration -InstanceId $InstanceId -Reason 'Terminated intentionally'

do {
    $Status = Get-DurableStatus -InstanceId $InstanceId -ShowHistory -ShowHistoryOutput -ShowInput
    Write-Host "Status: $($Status | ConvertTo-Json)"
    Start-Sleep -Seconds 5
} while ($Status.runtimeStatus -ne 'Terminated')

$Response = New-DurableOrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
Push-OutputBinding -Name Response -Value $Response

Write-Host 'InstanceManagement completed'
