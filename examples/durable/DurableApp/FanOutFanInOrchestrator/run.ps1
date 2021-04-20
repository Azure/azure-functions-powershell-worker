using namespace System.Net

param($Context)

Write-Host 'FanOutFanInOrchestrator: started.'

$parallelTasks =
    foreach ($Name in 'Tokyo', 'Seattle', 'London') {
        Invoke-DurableActivity -FunctionName 'SayHello' -Input $Name -NoWait
    }

$output = Wait-DurableTask -Task $parallelTasks

Write-Host 'FanOutFanInOrchestrator: finished.'

$output
