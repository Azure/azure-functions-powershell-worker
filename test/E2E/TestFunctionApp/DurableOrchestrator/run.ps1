using namespace System.Net

param($Context)

$ErrorActionPreference = 'Stop'

Write-Host "DurableOrchestrator: started. Input: $($Context.Input)"

# Function chaining
$output = @()
$output += Invoke-ActivityFunction -FunctionName "DurableActivity" -Input "Tokyo"

# Fan-out/Fan-in
$tasks = @()
$tasks += Invoke-ActivityFunction -FunctionName "DurableActivity" -Input "Seattle" -NoWait
$tasks += Invoke-ActivityFunction -FunctionName "DurableActivity" -Input "London" -NoWait
$output += Wait-DurableTask -Task $tasks

# Retries
$retryOptions = New-DurableRetryOptions -FirstRetryInterval (New-Timespan -Seconds 2) -MaxNumberOfAttempts 5
$inputData = @{ Name = 'Toronto'; StartTime = $Context.CurrentUtcDateTime }
$output += Invoke-ActivityFunction -FunctionName "DurableActivityFlaky" -Input $inputData -RetryOptions $retryOptions

Write-Host "DurableOrchestrator: finished."

return $output
