using namespace System.Net

param($Context)

$ErrorActionPreference = 'Stop'

Write-Host "CurrentUtcDateTimeOrchestrator started. Input: $($Context.Input)"

<<<<<<< HEAD
$activityResults = @()
=======
$output = @()
>>>>>>> 751b9f5... Added CurrentUtcDateTime E2E tests

$fileName = "$("{0:MM_dd_yyyy_hh_mm_ss.ff_zz}" -f $Context.CurrentUtcDateTime)test.txt"
$path = "$($env:TEMP)\$fileName"

Add-Content -Value '---' -Path $path

# Initial value of CurrentUtcDateTime
# <Timestamp1>
Add-Content -Value $Context.CurrentUtcDateTime -Path $path

# Checks that CurrentUtcDateTime does not update without restarting the orchestrator
# <Timestamp1>
Add-Content -Value $Context.CurrentUtcDateTime -Path $path

# Checks that CurrentUtcDateTime updates following a completed activity function
<<<<<<< HEAD
$activityResults += Invoke-ActivityFunction -FunctionName "DurableActivity" -Input "Tokyo"
=======
$output += Invoke-ActivityFunction -FunctionName "DurableActivity" -Input "Tokyo"
>>>>>>> 751b9f5... Added CurrentUtcDateTime E2E tests
# <Timestamp2>
Add-Content -Value $Context.CurrentUtcDateTime -Path $path

Write-Host "About to start asynchronous calls."

# Checks that CurrentUtcDateTime does not update following an asynchronous call
$tasks = @()
$tasks += Invoke-ActivityFunction -FunctionName "DurableActivity" -Input "Seattle" -NoWait
# <Timestamp2>
Add-Content -Value $Context.CurrentUtcDateTime -Path $path

$tasks += Invoke-ActivityFunction -FunctionName "DurableActivity" -Input "London" -NoWait
# <Timestamp2>
Add-Content -Value $Context.CurrentUtcDateTime -Path $path

Write-Host "Finished the asynchronous calls."

# Checks that CurrentUtcDateTime updates only after all awaited activity functions completed
<<<<<<< HEAD
$activityResults += Wait-ActivityFunction -Task $tasks
# <Timestamp3>
=======
# <Timestamp3>
$output += Wait-ActivityFunction -Task $tasks
>>>>>>> 751b9f5... Added CurrentUtcDateTime E2E tests
Add-Content -Value $Context.CurrentUtcDateTime -Path $path

Write-Host "CurrentUtcDateTimeOrchestrator: finished."

return $path

<#
<<<<<<< HEAD
Contents of the file should resemble one of the following two cases:
Case 1                      Case 2
Line                        Line
0     ---                   0     ---
1     <Timestamp1>          1     <Timestamp1>    
2     <Timestamp1>          2     <Timestamp1>
3     ---                   3     ---
4     <Timestamp1>          4     <Timestamp1>
5     <Timestamp1>          5     <Timestamp1>
6     <Timestamp2>          6     <Timestamp2>
7     <Timestamp2>          7     <Timestamp2>
8     <Timestamp2>          8     <Timestamp2>
9     ---                   9     ---
10    <Timestamp1>          10    <Timestamp1>
11    <Timestamp1>          11    <Timestamp1>
12    <Timestamp2>          12    <Timestamp2>
13    <Timestamp2>          13    <Timestamp2>
14    <Timestamp2>          14    <Timestamp2>
15    <Timestamp3>          15    ---
                            16    <Timestamp1>
                            17    <Timestamp1>
                            18    <Timestamp2>
                            19    <Timestamp2>
                            20    <Timestamp2>
                            21    <Timestamp3>
=======
Contents of the file should resemble:
Line
0     ---
1     <Timestamp1>
2     <Timestamp1>
3     ---
4     <Timestamp1>
5     <Timestamp1>
6     <Timestamp2>
7     <Timestamp2>
8     <Timestamp2>
9     ---
10    <Timestamp1>
11    <Timestamp1>
12    <Timestamp2>
13    <Timestamp2>
14    <Timestamp2>
15    ---
16    <Timestamp1>
17    <Timestamp1>
18    <Timestamp2>
19    <Timestamp2>
20    <Timestamp2>
21    <Timestamp3>
>>>>>>> 751b9f5... Added CurrentUtcDateTime E2E tests
#>
