param($req, $TriggerMetadata)

Write-Verbose "PowerShell HTTP trigger function processed a request."

if($req.Query.Name -or $req.Body.Name) {
    $name = $req.Query.Name
    if (-not $name) { $name = $req.Body.Name }

    $status = 200
    $body = "Hello " + $name
}
else {
    $status = 400
    $body = "Please pass a name on the query string or in the request body."
}

Push-OutputBinding -Name res -Value ([HttpResponseContext]@{
    StatusCode = $status
    Body = $body
})

