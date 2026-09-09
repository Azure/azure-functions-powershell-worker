# This file has a parse error - unterminated string
function Broken {
    [AzFunction()]
    param(
        [HttpTrigger()]
        $Request
    )
    "Hello
}
