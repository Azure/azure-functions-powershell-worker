using namespace System.Net

param($Context)

Write-Host 'FunctionChainingOrchestrator: started.'

$output = @()

$output += Invoke-DurableActivity -FunctionName 'SayHello' -Input 'Tokyo'
$output += Invoke-DurableActivity -FunctionName 'SayHello' -Input 'Seattle'
$output += Invoke-DurableActivity -FunctionName 'SayHello' -Input 'London'

Write-Host 'FunctionChainingOrchestrator: finished.'

$output
