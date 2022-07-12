param($Context)

$output = @()

$output += Start-DurableExternalEventListener -EventName "TESTEVENTNAME" 

$output
