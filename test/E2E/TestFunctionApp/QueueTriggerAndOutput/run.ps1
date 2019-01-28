param($myQueueItem)

Write-Host "PowerShell queue trigger function processed work item $myQueueItem"
Push-OutputBinding -Name outQueueItem -Value $myQueueItem
