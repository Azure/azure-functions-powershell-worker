param($Context)

Write-Host "MyOrchestrator: started. Input: $($Context.Input)"

$activityResult = Invoke-ActivityFunction -FunctionName "LongRunningActivity" -Input $Context.Input
Write-Host "MyOrchestrator: Returned from LongRunningActivity: '$activityResult'"

Write-Host "MyOrchestrator: finished."
return $activityResult
