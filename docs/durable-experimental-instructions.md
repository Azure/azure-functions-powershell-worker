# How to try experimental Durable PowerShell functions

## **PLEASE READ THIS:**

> This is an experimental feature. Do **not** enable or try to use it for production purposes. The programming model may change, so the sample code or any app code you come up with may not work in the next versions. The main purpose of making it available now is to enable experimentation and early feedback. **Your feedback is highly appreciated**, and please feel free to file [GitHub issues](https://github.com/Azure/azure-functions-powershell-worker/issues/new).

## 1. Sample app

Start with the sample durable app at `examples/durable/DurableApp`.

Note:

- Please make sure you are using Azure Functions **v3** runtime. There are no plans to support Durable PowerShell on Azure Functions **v1** or **v2**.
- There is no support for Durable Functions **2.x** at this point, only Durable Functions **1.x** are supported (see [Durable Functions versions overview](https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-versions)).
- Please make sure you are using the sample code version corresponding to the version of the PowerShell Worker. The programming model is still changing, so older or newer samples may not work. So, if you are trying Durable PowerShell on Azure, use the samples tagged with the version of the PowerShell worker deployed to Azure. Alternatively, take the latest PowerShell Worker code from the **dev** branch, and rebuild and run the PowerShell Worker locally.
- Only a limited number of patterns is enabled at this point:
  - [Function chaining](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-overview?tabs=csharp#chaining)
  - [Fan out/fan in](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-overview?tabs=csharp#fan-in-out)
  - [Async HTTP APIs](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-overview?tabs=csharp#async-http)

## 2. Install the required extensions

Before deploying your app, run the following command in the app directory:

``` bash
func extensions install --powershell
```

Please note that the Microsoft.Azure.WebJobs.Extensions.DurableTask package should be pinned to a 1.* version until Durable Functions 2.x support is added. For this reason, the extensions.csproj file already includes the following line:

``` xml
<PackageReference Include="Microsoft.Azure.WebJobs.Extensions.DurableTask" Version="1.8.3" />
```

## 3. App settings

Set the following app settings (if running on Azure) or just use the sample local.settings.json (if running locally):

``` json
{
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "AzureWebJobsFeatureFlags": "AllowSynchronousIO",
    "FUNCTIONS_WORKER_RUNTIME": "powershell",
    "PSWorkerInProcConcurrencyUpperBound": 10,
    "PSWorkerEnableExperimentalDurableFunctions": "true"
  }
}
```

Make sure [AzureWebJobsStorage](https://docs.microsoft.com/azure/azure-functions/functions-app-settings#azurewebjobsstorage) points to a valid storage account. This storage is required for data persisted by Durable Functions.

You may need to adjust PSWorkerInProcConcurrencyUpperBound to increase concurrency for Fan-out/Fan-in pattern.

## 4. Starting the app

Configure the environment variable FUNCTIONS_WORKER_RUNTIME_VERSION to select PowerShell 7:

``` PowerShell
$env:FUNCTIONS_WORKER_RUNTIME_VERSION = '~7'
```

and start the app:

``` bash
func start
```

## 5. Function Chaining pattern

Start the FunctionChainingStart function:

``` PowerShell
Invoke-RestMethod 'http://localhost:7071/api/FunctionChainingStart'
```

## 6. Fan-out/Fan-in pattern

Start the FanOutFanInStart function:

``` PowerShell
Invoke-RestMethod 'http://localhost:7071/api/FanOutFanInStart'
```

## 7. Async HTTP APIs

When you invoke FunctionChainingStart or FanOutFanInStart, it returns an HTTP 202 response with the orchestration management URLs, so you can start invoking statusQueryGetUri and wait for the orchestration to complete:

``` PowerShell
$invokeResponse = Invoke-RestMethod 'http://localhost:7071/api/FunctionChainingStart'
while ($true) {
    $status = Invoke-RestMethod $invokeResponse.statusQueryGetUri
    $status
    Start-Sleep -Seconds 2
    if ($status.runtimeStatus -eq 'Completed') {
        break;
    }
}
```
