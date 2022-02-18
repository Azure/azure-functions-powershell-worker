param($Context)

$output = @()

$Context.InstanceId
$Context.IsReplaying
$output += Invoke-DurableActivity -FunctionName 'DurableActivity' -Input $Context.InstanceId
$Context.IsReplaying
$output
