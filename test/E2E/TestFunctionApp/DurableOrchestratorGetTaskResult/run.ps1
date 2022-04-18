param($Context)

$output = @()

$task = Invoke-DurableActivity -FunctionName 'DurableActivity' -Input "world" -NoWait
$firstTask = Wait-DurableTask -Task @($task) -Any
$output += Get-DurableTaskResult -Task @($firstTask)
$output
