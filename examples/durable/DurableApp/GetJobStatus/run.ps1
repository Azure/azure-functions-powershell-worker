param($name)

if ((Get-Date).ToUniversalTime() -lt $name) {
    Write-Host "Not Completed"
}
else {
    Write-Host "Completed"
}