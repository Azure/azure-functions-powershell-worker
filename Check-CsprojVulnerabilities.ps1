param
(
    [String[]]
    $CsprojFilePath,

    [switch]
    $PrintReport
)

if (-not $CsprojFilePath)
{
    $CsprojFilePath = @(
        "$PSScriptRoot/src/Microsoft.Azure.Functions.PowerShellWorker.csproj"
        "$PSScriptRoot/test/Unit/Microsoft.Azure.Functions.PowerShellWorker.Test.csproj"
        "$PSScriptRoot/test/E2E/Azure.Functions.PowerShellWorker.E2E/Azure.Functions.PowerShellWorker.E2E/Azure.Functions.PowerShellWorker.E2E.csproj"
    )
}

$logFilePath = "$PSScriptRoot/build.log"

try
{
    foreach ($projectFilePath in $CsprojFilePath)
    {
        Write-Host "Analyzing '$projectFilePath' for vulnerabilities..."
        
        $projectFolder = Split-Path $projectFilePath
        
        Push-Location $projectFolder
        & { dotnet restore $projectFilePath }
        & { dotnet list $projectFilePath package --include-transitive --vulnerable } 3>&1 2>&1 > $logFilePath
        Pop-Location

        # Check and report if vulnerabilities are found
        $report = Get-Content $logFilePath -Raw
        $result = $report | Select-String "has no vulnerable packages given the current sources"

        if ($result)
        {
            Write-Host "No vulnerabilities found"
        }
        else
        {
            $output = [System.Environment]::NewLine + "Vulnerabilities found!"            
            if ($PrintReport.IsPresent)
            {
                $output += $report
            }
            
            Write-Host $output -ForegroundColor Red
            Exit 1
        }
        Write-Host ""
    }
}
finally
{
    if (Test-Path $logFilePath)
    {
        Remove-Item $logFilePath -Force
    }
}
