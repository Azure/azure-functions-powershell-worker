# PSWorkerPrototype
Prototype for Azure Functions PowerShell Language Worker

## Steps

1. Modify `DefaultExecutablePath` in `worker.config.json` (to something like this `"C:\\Program Files\\dotnet\\dotnet.exe"`)
2. `cd path/to/PSWorkerPrototype`
3. `dotnet publish`
4. Run:

```powershell
# Windows if you installed the Azure Functions Core Tools via npm
Remove-Item -Recurse -Force ~\AppData\Roaming\npm\node_modules\azure-functions-core-tools\bin\workers\powershell
Copy-Item src\bin\Debug\netcoreapp2.1\publish ~\AppData\Roaming\npm\node_modules\azure-functions-core-tools\bin\workers\powershell -Recurse -Force

# macOS if you installed the Azure Functions Core Tools via brew
Remove-Item -Recurse -Force /usr/local/Cellar/azure-functions-core-tools/2.0.1-beta.33/workers/powershell
Copy-Item src/bin/Debug/netcoreapp2.1/publish /usr/local/Cellar/azure-functions-core-tools/2.0.1-beta.33/workers/powershell -Recurse -Force
```