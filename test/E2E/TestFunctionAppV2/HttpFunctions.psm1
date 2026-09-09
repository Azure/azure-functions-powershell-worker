# HTTP Trigger Functions
# Groups: HttpTrigger, HttpTriggerThrows, HttpTriggerWithMetadata, UsingManagedDependencies

function HttpTrigger {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = "Anonymous", Methods = "GET, POST")]
        $req
    )

    Write-Verbose "PowerShell HTTP trigger function processed a request." -Verbose

    $name = $req.Query.Name
    if (-not $name) { $name = $req.Body.Name }

    if ($name) {
        $status = 200
        $body = "Hello " + $name
    }
    else {
        $status = 400
        $body = "Please pass a name on the query string or in the request body."
    }

    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = $status
        Body = $body
    })
}

function HttpTriggerThrows {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = "Anonymous", Methods = "GET, POST")]
        $req
    )

    Write-Host "PowerShell HTTP trigger function processed a request."
    throw "Test Exception"
}

function HttpTriggerWithMetadata {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = "Anonymous", Methods = "GET, POST")]
        $req,

        $TriggerMetadata
    )

    Write-Verbose "PowerShell HTTP trigger function processed a request." -Verbose

    $name = $req.Query.Name
    if (-not $name) { $name = $req.Body.Name }

    if ($name) {
        $status = 200

        $invocationId = $TriggerMetadata.InvocationId
        $funcDirectory = $TriggerMetadata.FunctionDirectory
        $funcName = $TriggerMetadata.FunctionName

        $FuncDirSameAsScriptRoot = $funcDirectory -eq $PSScriptRoot
        $InvocationIdNullOrEmpty = [string]::IsNullOrEmpty($invocationId)

        $body = "{0} {1} {2}" -f $funcName, $FuncDirSameAsScriptRoot, $InvocationIdNullOrEmpty
    }
    else {
        $status = 400
        $body = "Please pass a name on the query string or in the request body."
    }

    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = $status
        Body = $body
    })
}

function UsingManagedDependencies {
    [AzFunction()]
    param(
        [HttpTrigger(AuthLevel = "Anonymous", Methods = "GET, POST")]
        $req
    )

    Write-Verbose "PowerShell HTTP trigger function processed a request." -Verbose

    Import-Module Az.Accounts

    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        Body = Get-Module Az.Accounts | ForEach-Object Path
    })
}
