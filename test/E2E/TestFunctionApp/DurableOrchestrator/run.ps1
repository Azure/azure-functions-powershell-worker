using namespace System.Net

param($Context)

Write-Host "MyOrchestrator: started. Input: $($Context.Input)"

$output = @()

$output += Invoke-ActivityFunction -FunctionName "SayHello" -Input "Tokyo"
$output += Invoke-ActivityFunction -FunctionName "SayHello" -Input "Seattle"
$output += Invoke-ActivityFunction -FunctionName "SayHello" -Input "London"

Write-Host "MyOrchestrator: finished."

return $output
