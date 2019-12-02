param($name)

Write-Host "SayHello($name) started"
Start-Sleep -Seconds 5
Write-Host "SayHello($name) finished"
return "Hello $name"
