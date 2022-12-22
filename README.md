![azure-functions-logo][]

|Branch|Status|
|---|---|
|dev|[![Build Status](https://dev.azure.com/azfunc/Azure%20Functions/_apis/build/status/Azure.azure-functions-powershell-worker?branchName=dev)](https://dev.azure.com/azfunc/Azure%20Functions/_build/latest?definitionId=21&branchName=dev)|
|v4.x/ps7.2|[![Build Status](https://dev.azure.com/azfunc/Azure%20Functions/_apis/build/status/Azure.azure-functions-powershell-worker?branchName=v3.x%2Fps7)](https://dev.azure.com/azfunc/Azure%20Functions/_build/latest?definitionId=21&branchName=v4.x%2Fps7.2)|
|v4.x/ps7.0|[![Build Status](https://dev.azure.com/azfunc/Azure%20Functions/_apis/build/status/Azure.azure-functions-powershell-worker?branchName=v3.x%2Fps7)](https://dev.azure.com/azfunc/Azure%20Functions/_build/latest?definitionId=21&branchName=v4.x%2Fps7.0)|
|v3.x/ps7|[![Build Status](https://dev.azure.com/azfunc/Azure%20Functions/_apis/build/status/Azure.azure-functions-powershell-worker?branchName=v3.x%2Fps7)](https://dev.azure.com/azfunc/Azure%20Functions/_build/latest?definitionId=21&branchName=v3.x%2Fps7)|
|v3.x/ps6|[![Build Status](https://dev.azure.com/azfunc/Azure%20Functions/_apis/build/status/Azure.azure-functions-powershell-worker?branchName=v3.x%2Fps6)](https://dev.azure.com/azfunc/Azure%20Functions/_build/latest?definitionId=21&branchName=v3.x%2Fps6)|
|v2.x|[![Build Status](https://dev.azure.com/azfunc/Azure%20Functions/_apis/build/status/Azure.azure-functions-powershell-worker?branchName=v2.x)](https://dev.azure.com/azfunc/Azure%20Functions/_build/latest?definitionId=21&branchName=v2.x)|

[azure-functions-logo]: https://raw.githubusercontent.com/Azure/azure-functions-cli/master/src/Azure.Functions.Cli/npm/assets/azure-functions-logo-color-raster.png

# Azure Functions PowerShell Language Worker

This repository will host the PowerShell language worker implementation for Azure Functions.
We'll also be using it to track work items related to PowerShell support.
Please feel free to leave comments about any of the features and design patterns.

## Overview

PowerShell support for Functions is based on [PowerShell Core 7](https://github.com/powershell/powershell),
[Functions on Linux](https://blogs.msdn.microsoft.com/appserviceteam/2017/11/15/functions-on-linux-preview/),
and the [Azure Functions runtime V4](https://docs.microsoft.com/en-us/azure/azure-functions/functions-versions).

## What's available?

### Triggers / Bindings

* HTTP Trigger (Webhook)
* Timer Trigger
* Blob Trigger
* Queue Trigger

## What's coming?

* More triggers and bindings
* Tooling integration
* A bunch of other good things

## Contributing

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/)
or contact [opencode@microsoft.com](mailto:opencode@microsoft.com)
with any additional questions or comments.

## Building from source

### Prereqs

* [.NET 7.0 SDK](https://www.microsoft.com/net/download/visual-studio-sdks)

### Build

1. Clone this repository
2. `cd azure-functions-powershell-worker`
3. `./build.ps1`

> NOTE: If you're missing a dependency,
you will be asked to run `./build.ps1 -Bootstrap`

### Run & Debug

The PowerShell worker alone is not enough to establish the functions app, we also need the support from
[Azure Functions Host](https://github.com/Azure/azure-functions-host).
You may either use a published host CLI or use the in-development host.
But both of the methods require you to attach to the .NET process if you want a step-by-step debugging experience.

#### Published Host

First, follow the [instructions to install the Azure Functions Core Tools](https://github.com/Azure/azure-functions-core-tools#installing).
Then locate the `azure-functions-core-tools\bin\workers\` folder.
Here are a few hints on where it could be located:

On Windows if you installed via `npm`
```
~\AppData\Roaming\npm\node_modules\azure-functions-core-tools\bin\workers\
```
On macOS if you installed via `brew`
```
/usr/local/Cellar/azure-functions-core-tools/<version>/workers/
```

Under the `workers/powershell` folder, create a folder with the name `7.4` if it does not exist yet. Copy the result of the `publish` directory into the `workers/powershell/7.4` folder, and copy the `publish/worker.config.json` file into the `workers/powershell` folder:
```powershell
Copy-Item -Recurse -Force ./src/bin/Debug/net7.0/publish/ "/usr/local/Cellar/azure-functions-core-tools/$(func --version)/workers/powershell/7.4"
Copy-Item -Recurse -Force ./src/bin/Debug/net7.0/publish/worker.config.json "/usr/local/Cellar/azure-functions-core-tools/$(func --version)/workers/powershell"
```

> NOTE: if the powershell folder already exists, you should delete it or debugging won't work.

Then `cd` into a Function App with PowerShell as the worker runtime 
(NOTE: There's an example PowerShell Function App in the `examples` folder).

Set the environment variable `FUNCTIONS_WORKER_RUNTIME_VERSION` to `7.4`, or add this as an app setting to the `local.settings.json` file.

Lastly, run:

```
func start
```

> NOTE: If you would like to see more out of the logs, see the logging section in the
[Azure Functions Host repo](https://github.com/Azure/azure-functions-host/wiki/Authoring-&-Testing-Language-Extensions#logs).

#### Latest Host

A developer may also use the latest host code by cloning the git repository [Azure Functions Host](https://github.com/Azure/azure-functions-host).
Now you need to navigate to the root folder of the host project and build it through:

```sh
dotnet restore WebJobs.Script.sln
dotnet build WebJobs.Script.sln
```

After the build succeeded,
set the environment variable `"AzureWebJobsScriptRoot"`
to the root folder path (the folder which contains the `host.json`)
of your test functions app.

Under the `workers/powershell` folder, create a folder with the name `7.4` if it does not exist yet. Then copy the `publish` directory to `workers/powershell/7.4`, and the `publish/worker.config.json` to `workers/powershell`:
```powershell
Copy-Item -Recurse -Force ./src/bin/Debug/net7.0/publish/ "<Azure Functions Host Root>/src/WebJobs.Script.WebHost/bin/Debug/net7.0/workers/powershell/7.4"
Copy-Item -Force ./src/bin/Debug/net7.0/publish/worker.config.json "<Azure Functions Host Root>/src/WebJobs.Script.WebHost/bin/Debug/net7.0/workers/powershell"
```

Then you can start the host by running:
```sh
dotnet ./src/WebJobs.Script.WebHost/bin/Debug/net7.0/Microsoft.Azure.WebJobs.Script.WebHost.dll
```

> Note: Remember to remove `"AzureWebJobsScriptRoot"`
environment variable after you have finished debugging,
because it will also influence the `func` CLI tool.

### Packaging

To package the PowerShell Language Worker as a nupkg, do the following:

* `cd azure-functions-powershell-worker`
* `dotnet publish`
* `cd package`
* `dotnet pack`

That will place a `Microsoft.Azure.Functions.PowerShellWorker.*.nupkg` in:

`azure-functions-powershell-worker/package/bin/Debug`

It pulls the contents of the publish folder in:

`azure-functions-powershell-worker/src/bin/Debug/net7.0/publish`

if you specify a different Configuration or TargetFramework that will be honored.
