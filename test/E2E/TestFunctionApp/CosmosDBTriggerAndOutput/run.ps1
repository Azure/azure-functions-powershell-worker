param ($itemIn)

Write-Host "PowerShell Cosmos DB trigger function executed. Received document: $itemIn"

if ($itemIn -and $itemIn.Count -gt 0 ) {
    $doc = $itemIn[0]
    Write-Host "Document Id: $($doc.id)"
    $doc.Description = "testdescription"
    Push-OutputBinding -Name itemOut -Value $doc
}
