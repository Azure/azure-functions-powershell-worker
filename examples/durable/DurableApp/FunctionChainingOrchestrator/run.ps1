using namespace System.Net

param($Context)

Write-Host 'FunctionChainingOrchestrator: started.'

$output = @()

$output += Invoke-ActivityFunction -FunctionName 'SayHello' -Input 'Tokyo'
$output += Invoke-ActivityFunction -FunctionName 'SayHello' -Input 'Seattle'
$output += Invoke-ActivityFunction -FunctionName 'SayHello' -Input 'London'

Write-Host 'FunctionChainingOrchestrator: finished.'

$output
