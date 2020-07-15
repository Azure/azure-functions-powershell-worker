# How to try Durable PowerShell functions

## Please note

- This feature is getting ready for Public Preview. If you use it for production purposes, be aware that the programming model may change before GA, so the sample code or any app code you come up with may not work in the next versions.
- **Your feedback is highly appreciated**, please feel free to file [GitHub issues](https://github.com/Azure/azure-functions-powershell-worker/issues/new) if anything does not work as expected.
- Make sure you are using Azure Functions **v3** runtime. There are no plans to support Durable PowerShell on Azure Functions **v1** or **v2**.
- Only Durable Functions **2.x** will be officially supported (see [Durable Functions versions overview](https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-versions)). Technically, the current implementation works with Durable Functions **1.x** as well. However, it may stop working before GA.
- Only a limited number of patterns is enabled at this point:
  - [Function chaining](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-overview?tabs=csharp#chaining)
  - [Fan out/fan in](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-overview?tabs=csharp#fan-in-out)
  - [Async HTTP APIs](https://docs.microsoft.com/azure/azure-functions/durable/durable-functions-overview?tabs=csharp#async-http)

## Local setup

_This step is required if you intend to run Durable PowerShell functions on your local machine. Otherwise, feel free to skip this step._

At the moment of writing this (June 11, 2020), the PowerShell Worker version deployed by Azure Functions Core Tools does support Durable Functions yet. If you want to run Durable PowerShell functions locally, consider the following options:

- **Option 1**: Get started using a dev container with Visual Studio Code Remote for Containers or Visual Studio Online: https://github.com/anthonychu/powershell-durable-preview

- **Option 2**: Consider following these instructions as they automate and simplify the steps below: https://git.io/PS7DurableFunctionsNow

- **Option 3**: Install the latest PowerShell Worker build manually:

  - Install [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local?tabs=windows%2Ccsharp%2Cbash#install-the-azure-functions-core-tools) **v3.x** (if not installed yet).

  - Download the latest PowerShell worker package:

    ``` PowerShell
    Save-Package -Name 'Microsoft.Azure.Functions.PowershellWorker.PS7' -Source 'https://ci.appveyor.com/nuget/azure-functions-powershell-wor-0842fakagqy6/' -ProviderName NuGet -Path ~\Downloads\
    ```

  - Rename the downloaded .nuget file to .zip and extract the content of the `contentFiles\any\any\workers\powershell` folder to `workers\powershell` under the [Core Tools](https://github.com/Azure/azure-functions-powershell-worker/blob/dev/README.md#published-host), overwriting the existing files.

- **Option 4**: Wait for the next (higher than 3.0.2534) release of Azure Functions Core Tools.

## Azure setup

_This step is required if you intend to run Durable PowerShell functions on Azure. Otherwise, feel free to skip this step._

- Create a PowerShell Function app on Azure.
- Use the [instructions](https://github.com/Azure/azure-functions-powershell-worker/issues/371#issuecomment-641026259) to switch your app to PowerShell 7.

## Sample app

Start with the sample app at `examples/durable/DurableApp` in this repository.

### 1. Install extensions

Before deploying or starting it, run the following commands in the app directory:

``` bash
dotnet add package Microsoft.Azure.WebJobs.Extensions.DurableTask
func extensions install --powershell
```

### 2. Configure app settings

Set the following app settings (if running on Azure) or just use the sample local.settings.json (if running locally):
- Make sure `AzureWebJobsStorage` [points to a valid Azure storage account](https://docs.microsoft.com/azure/azure-functions/functions-app-settings#azurewebjobsstorage). This storage is required for data persisted by Durable Functions. When you create a new Function app on Azure, it normally points to an automatically provisioned storage account. If you intend to run the app locally, you can either keep the "UseDevelopmentStorage=true" value in the sample local.settings.json (in this case you will also need to install and start Azure Storage Emulator), or replace it with a connection string pointing to a real Azure storage account.
- `AzureWebJobsFeatureFlags` must contain `AllowSynchronousIO`. Don't ask.
- You may need to adjust `PSWorkerInProcConcurrencyUpperBound` to increase [concurrency](https://docs.microsoft.com/azure/azure-functions/functions-reference-powershell#concurrency) for the Fan-out/Fan-in pattern.

### 3. Deploy the app

If running locally, skip the step, otherwise deploy the app to Azure as you normally would.

### 4. Start the app

If running locally:
- If you have `UseDevelopmentStorage=true` as the `AzureWebJobsStorage` value, remember to start the Azure Storage Emulator first.
- Configure the environment variable FUNCTIONS_WORKER_RUNTIME_VERSION to select PowerShell 7:
  ``` PowerShell
  $env:FUNCTIONS_WORKER_RUNTIME_VERSION = '~7'
  ```
- Start the app:
  ``` bash
  func start
  ```

If running on Azure:
- Just start the app as you normally would.

### 5. Try Function Chaining pattern

Start the FunctionChainingStart function:

``` PowerShell
Invoke-RestMethod 'http://localhost:7071/api/FunctionChainingStart'
```

### 6. Try Fan-out/Fan-in pattern

Start the FanOutFanInStart function:

``` PowerShell
Invoke-RestMethod 'http://localhost:7071/api/FanOutFanInStart'
```

### 7. Try Async HTTP APIs pattern

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

### 8. Have fun, experiment, participate!

Please let us know how you use Durable PowerShell functions and what does not (_yet_) work as you expect. **Your feedback is highly appreciated**, please feel free to file [GitHub issues](https://github.com/Azure/azure-functions-powershell-worker/issues/new).
