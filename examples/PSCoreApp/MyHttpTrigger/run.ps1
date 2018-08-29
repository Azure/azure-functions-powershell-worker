param($req, $TriggerMetadata)

# Write-Host $TriggerMetadata["Name"]

# Invoked with Invoke-RestMethod:
# irm http://localhost:7071/api/MyHttpTrigger?Name=Tyler
# Input bindings are added to the scope of the script: ex. `$req`

# If no name was passed by query parameter
$name = 'World'

# You can interact with query parameters, the body of the request, etc.
if($req.Query.Name) {
    $name = $req.Query.Name
}

# you can write to the same streams as you would in a normal PowerShell script
Write-Verbose "Verbose $name" -Verbose
Write-Warning "Warning $name"

# items in the pipeline get logged
$name

# You set the value of your output bindings by assignment `$nameOfOutputBinding = 'foo'`
$res = [HttpResponseContext]@{
    Body = @{ Hello = $name }
    ContentType = 'application/json'
}