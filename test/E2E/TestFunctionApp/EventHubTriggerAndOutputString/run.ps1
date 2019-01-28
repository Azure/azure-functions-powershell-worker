param($eventHubMessages)

Write-Host "PowerShell eventhub trigger function called for object message array $eventHubMessages"

$eventHubMessages | ForEach-Object { "Processed message $_, value: $($_.value)" }

Push-OutputBinding -Name outEventHubMessage -Value $eventHubMessages[0]
