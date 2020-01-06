param($name)

Write-Host "DurableActivity($name) started"
Start-Sleep -Seconds 5
Write-Host "DurableActivity($name) finished"
return "Hello $name"
