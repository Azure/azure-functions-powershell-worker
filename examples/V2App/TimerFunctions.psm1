# Timer Trigger function using the V2 attribute-based programming model.

function CleanupJob {
    [AzFunction(Name = 'CleanupJob')]
    param(
        [TimerTrigger(Schedule = '0 0 */6 * * *')]
        $Timer
    )

    Write-Information "Cleanup job triggered at: $(Get-Date)"

    if ($Timer.IsPastDue) {
        Write-Warning 'Timer is running late!'
    }

    # Add your cleanup logic here, for example:
    # Remove-Item -Path "$env:TEMP\my-app-cache\*" -Recurse -Force -ErrorAction SilentlyContinue
    Write-Information 'Cleanup job completed.'
}
