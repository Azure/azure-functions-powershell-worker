# Azure Functions PowerShell examples

This is a collection of example Functions that use PowerShell as the language. They are enumerated below:

* _MyHttpTrigger_ - The "hello world" of Functions. This simply reads from the query params or body and responds with 'Hello X'
* _GetAzureVmHttpTrigger_ - An example that uses the Az module to fetch VMs. The correct environment variables need to be set for this to work
* _MyHttpTriggerWithModule_ - This gives an example of using a module that is added to the Function App's well-known "Modules" folder

## Setup

There is currently only one example with a dependency so you can simply use PowerShellGet's `Save-Module`:

```powershell
Set-Location PSCoreApp
Save-Module Coin ./Modules
```
or you can use PSDepend:
```powershell
Set-Location PSCoreApp
Invoke-PSDepend
```
