using namespace System.Net

param($Context)

Write-Host 'CustomStatusOrchestrator: started.'

$output = @()

Set-DurableCustomStatus -CustomStatus 'Processing Tokyo'
$output += Invoke-ActivityFunction -FunctionName 'SayHello' -Input 'Tokyo'

Set-DurableCustomStatus -CustomStatus @{ ProgressMessage = 'Processing Seattle'; Stage = 2 }
$output += Invoke-ActivityFunction -FunctionName 'SayHello' -Input 'Seattle'

Set-DurableCustomStatus -CustomStatus @('Processing London', 'Last stage')
$output += Invoke-ActivityFunction -FunctionName 'SayHello' -Input 'London'

Set-DurableCustomStatus 'Processing completed'

Write-Host 'CustomStatusOrchestrator: finished.'

$output
