param ($Req, $TriggerMetadata)

# Used for logging tests
Write-Verbose "a log"

Push-OutputBinding -Name res -Value $TriggerMetadata.Req