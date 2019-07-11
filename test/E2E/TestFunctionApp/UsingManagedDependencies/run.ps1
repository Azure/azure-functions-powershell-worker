param($req)

Write-Verbose "PowerShell HTTP trigger function processed a request." -Verbose

Import-Module Az.Accounts

Push-OutputBinding -Name res -Value ([HttpResponseContext]@{
    Body = Get-Module Az.Accounts | % Path
})
