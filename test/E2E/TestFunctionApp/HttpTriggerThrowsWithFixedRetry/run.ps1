param($req, $retryContext)

Write-Host "PowerShell HTTP trigger function processed a request."
Write-Host "Current retry count: $($retryContext.RetryCount)"

throw "Test Exception"
