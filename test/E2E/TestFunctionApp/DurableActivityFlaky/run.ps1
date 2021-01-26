param([hashtable]$InputData) 

# Intentional intermittent error, eventually "self-healing"
$elapsedTime = (Get-Date).ToUniversalTime() - $InputData.StartTime
if ($elapsedTime.TotalSeconds -lt 3) {
    throw 'Nope, no luck this time...'
}

"Hello $($InputData.Name)"
