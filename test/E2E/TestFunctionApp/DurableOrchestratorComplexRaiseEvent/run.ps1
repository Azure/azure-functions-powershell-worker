param($Context)

$output = @()

Invoke-DurableActivity -FunctionName "DurableActivity" -Input "Tokyo"
Invoke-DurableActivity -FunctionName "DurableActivity" -Input "Seattle"
$output += Start-DurableExternalEventListener -EventName "TESTEVENTNAME" 
Invoke-DurableActivity -FunctionName "DurableActivity" -Input "London"

$output
