function MultipleFunctions_First {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = "Function", Methods = "GET", Route = "first")]
        $Request
    )
    "Hello from First"
}

function MultipleFunctions_Second {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = "Anonymous", Methods = "POST", Route = "second")]
        $Request
    )
    "Hello from Second"
}
