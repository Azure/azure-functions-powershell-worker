using namespace System.Net

param($Context)

Write-Host 'FanOutFanInOrchestrator: started.'

$parallelTasks =
    foreach ($Name in 'Tokyo', 'Seattle', 'London') {
        Invoke-ActivityFunction -FunctionName 'SayHello' -Input $Name -NoWait
    }

$output = Wait-ActivityFunction -Task $parallelTasks

Write-Host 'FanOutFanInOrchestrator: finished.'

$output
