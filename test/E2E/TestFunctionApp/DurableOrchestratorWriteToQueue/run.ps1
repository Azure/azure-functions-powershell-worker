using namespace System.Net

param($Context)

Invoke-DurableActivity -FunctionName 'DurableActivityWritesToQueue' -Input 'QueueData'