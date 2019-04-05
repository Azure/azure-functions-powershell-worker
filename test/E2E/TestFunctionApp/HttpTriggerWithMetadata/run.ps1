param($req, $triggerMetadata)

# You can write to the Azure Functions log streams as you would in a normal PowerShell script.
Write-Verbose "PowerShell HTTP trigger function processed a request." -Verbose

# You can interact with query parameters, the body of the request, etc.
$name = $req.Query.Name
if (-not $name) { $name = $req.Body.Name }

if($name) {
    $status = 200

    $invocationId = $triggerMetadata.InvocationId
    $funcDirectory = $triggerMetadata.FunctionDirectory
    $funcName = $triggerMetadata.FunctionName

    $FuncDirSameAsScriptRoot = $funcDirectory -eq $PSScriptRoot
    $InvocationIdNullOrEmpty = [string]::IsNullOrEmpty($invocationId)

    $body = "{0} {1} {2}" -f $funcName, $FuncDirSameAsScriptRoot, $InvocationIdNullOrEmpty
}
else {
    $status = 400
    $body = "Please pass a name on the query string or in the request body."
}

# You associate values to output bindings by calling 'Push-OutputBinding'.
Push-OutputBinding -Name res -Value ([HttpResponseContext]@{
    StatusCode = $status
    Body = $body
})
