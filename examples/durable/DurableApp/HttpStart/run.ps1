using namespace System.Net

param($Request, $TriggerMetadata)

Write-Host 'HttpStart started'

$InstanceId = Start-NewOrchestration -FunctionName $Request.Params.FunctionName -InputObject $Request.Query.Input
Write-Host "Started orchestration $($Request.Params.FunctionName) with ID = '$InstanceId'"

$Response = New-DurableOrchestrationCheckStatusResponse -Request $Request -InstanceId $InstanceId
Push-OutputBinding -Name Response -Value $Response

Write-Host 'HttpStart completed'
