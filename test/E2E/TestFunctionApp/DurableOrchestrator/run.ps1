using namespace System.Net

param($Context)

$ErrorActionPreference = 'Stop'

Write-Host "DurableOrchestrator: started. Input: $($Context.Input)"

$output = @()

$output += Invoke-ActivityFunction -FunctionName "DurableActivity" -Input "Tokyo"
$output += Invoke-ActivityFunction -FunctionName "DurableActivity" -Input "Seattle"
$output += Invoke-ActivityFunction -FunctionName "DurableActivity" -Input "London"

Write-Host "DurableOrchestrator: finished."

return $output
