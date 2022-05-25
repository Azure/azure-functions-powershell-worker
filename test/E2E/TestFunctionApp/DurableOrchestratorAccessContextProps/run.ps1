param($Context)

$output = @()

$output += $Context.IsReplaying
$output += Invoke-DurableActivity -FunctionName 'DurableActivity' -Input $Context.InstanceId
$output += $Context.IsReplaying
$output
