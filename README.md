# Azure Functions PowerShell Language Worker

This repository will host the PowerShell language worker implementation for Azure Functions. We'll also be using it to track work items related to PowerShell support. Please feel free to leave comments about any of the features and design patterns.

> 🚧 The project is currently work in progress. Please do not use in production as we expect developments over time. To receive important updates, including breaking changes announcements, watch the Azure App Service announcements repository. 🚧

## Overview

PowerShell support for Functions is based on [PowerShell Core 6.1](https://github.com/powershell/powershell), [Functions on Linux](https://blogs.msdn.microsoft.com/appserviceteam/2017/11/15/functions-on-linux-preview/), and the [Azure Functions runtime V2](https://docs.microsoft.com/en-us/azure/azure-functions/functions-versions).

What's available?

* Triggers / Bindings : HTTP/Webhook

What's coming?

* More triggers and bindings
* A bunch of other good things

## Contributing

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Building from source

### Prereqs

* [.NET 2.1 SDK](https://www.microsoft.com/net/download/visual-studio-sdks)

### Build

* Clone this repository
* `cd azure-functions-powershell-worker`
* `dotnet publish`

### Run & Debug

The PowerShell worker alone is not enough to establish the functions app, we also need the support from [Azure Functions Host](https://github.com/Azure/azure-functions-host). You may either use a published host CLI or use the in-development host. But both of the methods require you to attach to the .NET process if you want a step-by-step debugging experience.

#### Published Host

You can install the latest Azure functions CLI tool by:

```sh
npm install -g azure-functions-core-tools@core
```

By default, the binaries are located in `"<Home Folder>/.azurefunctions/bin"`. Copy the `"<Azure Functions PowerShell Worker Root>/azure-functions-powershell-worker/src/bin/Debug/netcoreapp2.1/publish"` folder to `"<Home Folder>/.azurefunctions/bin/workers/powershell/"`. And start it normally using:

```sh
func start
```

#### Latest Host

A developer may also use the latest host code by cloning the git repository [Azure Functions Host](https://github.com/Azure/azure-functions-host). Now you need to navigate to the root folder of the host project and build it through:

```sh
dotnet restore WebJobs.Script.sln
dotnet build WebJobs.Script.sln
```

After the build succeeded, set the environment variable `"AzureWebJobsScriptRoot"` to the root folder path (the folder which contains the `host.json`) of your test functions app; and copy the `"<Azure Functions PowerShell Worker Root>/azure-functions-powershell-worker/src/bin/Debug/netcoreapp2.1/publish"` folder to `"<Azure Functions Host Root>/src/WebJobs.Script.WebHost/bin/Debug/netcoreapp2.0/workers/powershell"`. Now it's time to start the host:

```sh
dotnet ./src/WebJobs.Script.WebHost/bin/Debug/netcoreapp2.0/Microsoft.Azure.WebJobs.Script.WebHost.dll
```

> Note: Remember to remove `"AzureWebJobsScriptRoot"` environment variable after you have finished debugging, because it will also influence the `func` CLI tool.
