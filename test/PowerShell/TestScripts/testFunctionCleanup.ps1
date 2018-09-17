param ($Req)

if(!$global:foo)
{
    $global:foo = "is not set"
}

Push-OutputBinding -Name res -Value $global:foo

$global:foo = "is set"
