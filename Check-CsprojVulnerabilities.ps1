<#
.SYNOPSIS
Checks project dependencies for known vulnerabilities.

.PARAMETER AuditSource
Uses the specified NuGet V3 source only for vulnerability data. Package restores continue to use NuGet.config.

.EXAMPLE
./Check-CsprojVulnerabilities.ps1 -AuditSource https://data.nuget.org/v3/index.json
#>

param
(
    [String[]]
    $CsprojFilePath,

    [String]
    $AuditSource,

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
$auditConfigFilePath = $null
$auditWarningReported = $false

try
{
    if ($AuditSource)
    {
        $auditConfigFilePath = Join-Path ([System.IO.Path]::GetTempPath()) "nuget-audit-$([guid]::NewGuid()).config"
        $escapedAuditSource = [System.Security.SecurityElement]::Escape($AuditSource)
        @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
  </packageSources>
  <auditSources>
    <clear />
    <add key="manual" value="$escapedAuditSource" />
  </auditSources>
</configuration>
"@ | Set-Content -LiteralPath $auditConfigFilePath -Encoding utf8
    }

    foreach ($projectFilePath in $CsprojFilePath)
    {
        $projectFilePath = (Resolve-Path $projectFilePath).Path
        Write-Host "Analyzing '$projectFilePath' for vulnerabilities..."
        
        $projectFolder = Split-Path $projectFilePath
        $restoreArguments = @("restore", $projectFilePath)
        $listArguments = @("list", $projectFilePath, "package", "--include-transitive", "--vulnerable")

        if ($AuditSource)
        {
            $restoreArguments += "-p:NuGetAudit=false"
            $listArguments += @("--no-restore", "--config", $auditConfigFilePath)
        }
        
        Push-Location $projectFolder
        & dotnet @restoreArguments
        & { dotnet @listArguments } 3>&1 2>&1 > $logFilePath
        Pop-Location

        # Check and report if vulnerabilities are found
        $report = Get-Content $logFilePath -Raw
        if (-not $auditWarningReported -and $report -match '\bNU1905\b')
        {
            Write-Warning "NuGet audit source did not provide vulnerability data (NU1905). Vulnerability results may be incomplete."
            $auditWarningReported = $true
        }

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

    if ($auditConfigFilePath -and (Test-Path $auditConfigFilePath))
    {
        Remove-Item $auditConfigFilePath -Force
    }
}
