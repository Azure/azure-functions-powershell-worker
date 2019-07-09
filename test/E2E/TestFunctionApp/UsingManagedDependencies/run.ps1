param($req)

Write-Verbose "PowerShell HTTP trigger function processed a request." -Verbose

Push-OutputBinding -Name res -Value ([HttpResponseContext]@{
    Body = Get-Module Az -ListAvailable | % Path
})
