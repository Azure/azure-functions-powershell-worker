param($name)

Write-Host "DurableActivity($name) started"
Start-Sleep -Seconds 10
Write-Host "DurableActivity($name) finished"

"Hello $name"
