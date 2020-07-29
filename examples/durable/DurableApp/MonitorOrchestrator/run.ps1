using namespace System.Net

param($Context)

Write-Host 'MonitorOrchestrator: started.'

$output = @()

$jobId = $Context.Input.JobId
$machineId = $Context.Input.MachineId
$pollingInterval = New-TimeSpan -Seconds $Context.Input.PollingInterval
$expiryTime = $Context.Input.ExpiryTime

while ($Context.CurrentUtcDateTime -lt $expiryTime) {
    $jobStatus = Invoke-ActivityFunction -FunctionName 'GetJobStatus' -Input $jobId
    if ($jobStatus -eq "Completed") {
        # Perform an action when a condition is met.
        $output += Invoke-ActivityFunction -FunctionName 'SendAlert' -Input $machineId
        break
    }

    # Orchestration sleeps until this time.
    Start-DurableTimer -Duration $pollingInterval
}

# Perform more work here, or let the orchestration end.
Write-Host 'MonitorOrchestrator: finished.'

$output