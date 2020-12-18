using namespace System.Net

param($Context)

$ErrorActionPreference = 'Stop'

Invoke-ActivityFunction -FunctionName 'DurableActivityWithException' -Input 'Name' -ErrorAction Stop

'This should not be returned'
