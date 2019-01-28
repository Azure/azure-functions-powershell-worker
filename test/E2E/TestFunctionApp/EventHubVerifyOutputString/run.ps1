param($eventHubMessages)

Write-Host "PowerShell EventHubVerifyStringObject function called for message $eventHubMessages"
Push-OutputBinding -Name outEventHubMessage -Value $eventHubMessages
