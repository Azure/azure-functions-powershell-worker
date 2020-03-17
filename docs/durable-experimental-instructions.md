# How to try experimental Durable PowerShell functions

## **PLEASE READ THIS:**

> This is an experimental feature. Do **not** enable or try to use it for production purposes. The programming model may change, so the sample code or any app code you come up with may not work in the next versions. The main purpose of making it available now is to enable experimentation and early feedback. **Your feedback is highly appreciated**, and please feel free to file [GitHub issues](https://github.com/Azure/azure-functions-powershell-worker/issues/new).

## 1. PowerShell worker version

At the moment of writing this (March 14, 2020), the Durable PowerShell implementation deployed to Azure is outdated. If you want to follow the instructions below, please install a more recent version locally:
- Install [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local?tabs=windows%2Ccsharp%2Cbash#install-the-azure-functions-core-tools) **v3.x** (if not installed yet).
- Download the [PowerShell worker package](7o8dueat6aj7d2qc/artifacts/Microsoft.Azure.Functions.PowerShellWorker.PS7.3.0.261.nupkg), rename it to .zip, and extract the content of the `contentFiles\any\any\workers\powershell` folder to `workers\powershell` under the [Core Tools](https://github.com/Azure/azure-functions-powershell-worker/blob/dev/README.md#published-host), overwriting the existing files.

If you want to try this on Azure, you will have to wait a bit longer.

## 2. Sample app

Start with the sample durable app at `examples/durable/DurableApp`.

Note:

- Please make sure you are using Azure Functions **v3** runtime. There are no plans to support Durable PowerShell on Azure Functions **v1** or **v2**.
- Only Durable Functions **2.x** are supported (see [Durable Functions versions overview](https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-versions)). There is no plan to keep Durable Functions **1.x** support.
- Please make sure you are using the sample code version corresponding to the version of the PowerShell Worker. The programming model is still changing, so older or newer samples may not work. So, if you are trying Durable PowerShell on Azure, use the samples tagged with the version of the PowerShell worker deployed to Azure. Alternatively, take the latest PowerShell Worker code from the **dev** branch, and rebuild and run the PowerShell Worker locally.
- Only a limited number of patterns is enabled at this point:
  - [Function chaining](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-overview?tabs=csharp#chaining)
  - [Fan out/fan in](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-overview?tabs=csharp#fan-in-out)
  - [Async HTTP APIs](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-overview?tabs=csharp#async-http)

## 3. Install the required extensions

Before deploying your app, run the following command in the app directory:

``` bash
func extensions install --powershell
```

## 4. App settings

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

- Make sure `AzureWebJobsStorage` [points to a valid Azure storage account](https://docs.microsoft.com/azure/azure-functions/functions-app-settings#azurewebjobsstorage). This storage is required for data persisted by Durable Functions.
- `AzureWebJobsFeatureFlags` must contain `AllowSynchronousIO`. Don't ask.
- `FUNCTIONS_WORKER_RUNTIME` must be set to `powershell`.
- You may need to adjust `PSWorkerInProcConcurrencyUpperBound` to increase [concurrency](https://docs.microsoft.com/azure/azure-functions/functions-reference-powershell#concurrency) for the Fan-out/Fan-in pattern.
- `PSWorkerEnableExperimentalDurableFunctions` is a feature flag that enables PowerShell Durable Functions. It is turned off by default for now.

## 5. Starting the app

If you have `UseDevelopmentStorage=true` as the `AzureWebJobsStorage` value, remember to start the Azure Storage Emulator.

Configure the environment variable FUNCTIONS_WORKER_RUNTIME_VERSION to select PowerShell 7:

``` PowerShell
$env:FUNCTIONS_WORKER_RUNTIME_VERSION = '~7'
```

and start the app:

``` bash
func start
```

## 6. Function Chaining pattern

Start the FunctionChainingStart function:

``` PowerShell
Invoke-RestMethod 'http://localhost:7071/api/FunctionChainingStart'
```

## 7. Fan-out/Fan-in pattern

Start the FanOutFanInStart function:

``` PowerShell
Invoke-RestMethod 'http://localhost:7071/api/FanOutFanInStart'
```

## 8. Async HTTP APIs

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
