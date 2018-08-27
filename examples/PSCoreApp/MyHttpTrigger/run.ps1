$name = 'World'
if($req.Query.Name) {
    $name = $req.Query.Name
}

Write-Verbose "Hello $name" -Verbose
Write-Warning "Warning $name"

$res = [HttpResponseContext]@{
    Body = @{ Hello = $name }
    ContentType = 'application/json'
}