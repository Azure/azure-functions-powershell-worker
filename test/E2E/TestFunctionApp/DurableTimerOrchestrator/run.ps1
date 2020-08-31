using namespace System.Net

param($Context)

$ErrorActionPreference = 'Stop'

Write-Host "DurableTimerOrchestrator: started."

$tempFile = New-TemporaryFile
$tempDir = $tempFile.Directory.FullName
Remove-Item $tempFile
$fileName = "$("{0:MM_dd_yyyy_hh_mm_ss}" -f $Context.CurrentUtcDateTime)_timer_test.txt"
$path = Join-Path -Path $tempDir -ChildPath $fileName

Add-Content -Value "---" -Path $path

Add-Content -Value $Context.CurrentUtcDateTime -Path $path
# <Timestamp1>

Start-DurableTimer -Duration (New-TimeSpan -Seconds 5)

Add-Content -Value $Context.CurrentUtcDateTime -Path $path
# <Timestamp2>

Write-Host "DurableTimerOrchestrator: finished."

return (Get-Content $path) -join "`n"

<#
Contents of the file should resemble the following:
Line
0     ---
1     <Timestamp1>
2     ---
3     <Timestamp1>
4     <Timestamp2>
#>
