param($req)

$req.StatusCode = "201"
$req.Body.String = "hi"
return [PSCustomObject]@{
    res = $req
}