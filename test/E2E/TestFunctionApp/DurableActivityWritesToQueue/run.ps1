param($name)

Write-Information "Pushing to outputQueueItem output binding"
Push-OutputBinding -Name outputQueueItem -Value $name
Write-Information "Done"

"Hello $name!"