param($Context)

$output = @()

$task = Invoke-DurableActivity -FunctionName 'DurableActivity' -Input "mundo" -NoWait
$firstTask = Wait-DurableTask -Task @($task) -Any
$output += Get-DurableTaskResult $firstTask
$output
