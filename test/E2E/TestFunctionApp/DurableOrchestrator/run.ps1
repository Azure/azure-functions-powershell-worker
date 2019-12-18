using namespace System.Net

param($Context)

$ErrorActionPreference = 'Stop'

Write-Host "DurableOrchestrator: started. Input: $($Context.Input)"

$output = @()

$output += Invoke-ActivityFunction -FunctionName "SayHello" -Input "Tokyo"
$output += Invoke-ActivityFunction -FunctionName "SayHello" -Input "Seattle"
$output += Invoke-ActivityFunction -FunctionName "SayHello" -Input "London"

Write-Host "DurableOrchestrator: finished."

return $output
