function DurableOrchExample {
    [AzFunction()]
    param(
        [OrchestrationTrigger()]
        $Context
    )
    $output = Invoke-DurableActivity -FunctionName "MyActivity" -Input "test"
    return $output
}

function DurableActivityExample {
    [AzFunction()]
    param(
        [ActivityTrigger()]
        $name
    )
    "Hello $name"
}

function DurableClientExample {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = "Anonymous", Methods = "POST", Route = "start")]
        $Request,

        [DurableClient()]
        $starter
    )
    $InstanceId = Start-DurableOrchestration -FunctionName "DurableOrchExample"
    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{ Body = $InstanceId })
}
