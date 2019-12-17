# How to try experimental Durable PowerShell functions

## **PLEASE READ THIS:**

> This is an experimental feature. Do **not** enable or try to use it for production purposes. The programming model **will** change, so the sample code or any app code you come up with may not work in the next versions. The main purpose of making it available now is to enable experimentation and early feedback. **Your feedback is highly appreciated**, and please feel free to file GitHub issues. But don't expect backward compatibility or quick bugfixes at this point.

## 1. Function Chaining sample

Start with the sample Function Chaining app at `examples/durable/FunctionChainingApp`.

Please note:

- Please make sure you are using Azure Functions **v2** runtime. This does **not** currently work on Azure Functions **v3**, and will probably never work on **v1**.
- Please make sure you are using the sample code version corresponding to the version of the PowerShell Worker. The programming model is still changing, so older or newer samples may not work. As of December 12, 2019, **v1.0.197** is deployed on Azure. So, if you are trying online, use the samples tagged with **v1.0.197** (<https://github.com/Azure/azure-functions-powershell-worker/tree/v1.0.197/examples/durable/FunctionChainingApp>). Alternatively, take the latest PowerShell Worker code from the **dev** branch, and rebuild and run the PowerShell Worker locally.
- Function Chaining is the only implemented durable pattern at this point. We are currently working on the other patterns. Expect "Fan-Out/Fan-In" to be the next one.

## 2. Install the required extensions

Before deploying your app, run the following command in the app directory:

``` bash
func extensions install
```

## 3. Enable the experimental feature

Configure the following app setting:

`PSWorkerEnableExperimentalDurableFunctions` = `"true"`

## 4. Configure AzureWebJobsStorage

Make sure [AzureWebJobsStorage](https://docs.microsoft.com/azure/azure-functions/functions-app-settings#azurewebjobsstorage) points to a valid storage account. This storage is required for data persisted by Durable Functions.

## 5. Start

Start the HttpTrigger function. The HTTP trigger invocation will return the HTTP 202 response with the orchestration management URLs, so you can start invoking statusQueryGetUri.
