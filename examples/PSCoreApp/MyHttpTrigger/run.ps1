function FunctionName {
    $global:res = $req.GetHttpResponseContext()
    "hello verbose"
    $res.Json('{"Hello":"World"}')
    $res.SetHeader("foo", "bar")
}