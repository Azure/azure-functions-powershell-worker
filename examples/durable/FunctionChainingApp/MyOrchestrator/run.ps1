using namespace System.Net

param($Context)

Write-Host "MyOrchestrator: started. Input: $($Context.Input)"

$output = @()

$output += Invoke-ActivityFunctionAsync -FunctionName "SayHello" -Input "Tokyo"
$output += Invoke-ActivityFunctionAsync -FunctionName "SayHello" -Input "Seattle" -Verbose
$output += Invoke-ActivityFunctionAsync -FunctionName "SayHello" -Input "London" -Verbose

Write-Host "MyOrchestrator: finished."

return $output
