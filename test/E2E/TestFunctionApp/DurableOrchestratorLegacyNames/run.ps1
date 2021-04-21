using namespace System.Net

param($Context)

$ErrorActionPreference = 'Stop'

Write-Host "DurableOrchestratorLegacyNames: started. Input: $($Context.Input)"

Invoke-ActivityFunction -FunctionName "DurableActivity" -Input "Tokyo"

Write-Host "DurableOrchestratorLegacyNames: finished."

return $output
