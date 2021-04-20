using namespace System.Net

param($Context)

$ErrorActionPreference = 'Stop'

Write-Host "DurableOrchestrator: started. Input: $($Context.Input)"

Set-DurableCustomStatus -CustomStatus 'Custom status: started'

# Function chaining
$output = @()
$output += Invoke-DurableActivity -FunctionName "DurableActivity" -Input "Tokyo"

# Fan-out/Fan-in
$tasks = @()
$tasks += Invoke-DurableActivity -FunctionName "DurableActivity" -Input "Seattle" -NoWait
$tasks += Invoke-DurableActivity -FunctionName "DurableActivity" -Input "London" -NoWait
$output += Wait-DurableTask -Task $tasks

# Retries
$retryOptions = New-DurableRetryOptions -FirstRetryInterval (New-Timespan -Seconds 2) -MaxNumberOfAttempts 5
$inputData = @{ Name = 'Toronto'; StartTime = $Context.CurrentUtcDateTime }
$output += Invoke-DurableActivity -FunctionName "DurableActivityFlaky" -Input $inputData -RetryOptions $retryOptions

Set-DurableCustomStatus -CustomStatus 'Custom status: finished'

Write-Host "DurableOrchestrator: finished."

return $output
