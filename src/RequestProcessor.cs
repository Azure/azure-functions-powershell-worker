//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

using System.Management.Automation;
using Microsoft.Azure.Functions.PowerShellWorker.Messaging;
using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace  Microsoft.Azure.Functions.PowerShellWorker
{
    internal class RequestProcessor
    {
        private readonly MessagingStream msgStream;
        private readonly RpcLogger logger;
        private readonly PowerShellManager powerShellManager;
        private readonly FunctionLoader functionLoader;

        internal RequestProcessor(MessagingStream msgStream)
        {
            this.msgStream = msgStream;
            logger = new RpcLogger(msgStream);
            powerShellManager = PowerShellManager.Create(logger);
            functionLoader = new FunctionLoader();
        }

        internal async Task ProcessRequestLoop()
        {
            using (msgStream)
            {
                StreamingMessage request, response;
                while (await msgStream.MoveNext())
                {
                    request = msgStream.GetCurrentMessage();
                    switch (request.ContentCase)
                    {
                        case StreamingMessage.ContentOneofCase.WorkerInitRequest:
                            response = ProcessWorkerInitRequest(request);
                            break;

                        case StreamingMessage.ContentOneofCase.FunctionLoadRequest:
                            response = ProcessFunctionLoadRequest(request);
                            break;

                        case StreamingMessage.ContentOneofCase.InvocationRequest:
                            response = ProcessInvocationRequest(request);
                            break;

                        default:
                            throw new InvalidOperationException($"Not supportted message type: {request.ContentCase}");
                    }

                    await msgStream.WriteAsync(response);
                }
            }
        }

        internal StreamingMessage ProcessWorkerInitRequest(StreamingMessage request)
        {
            return new StreamingMessage()
            {
                RequestId = request.RequestId,
                WorkerInitResponse = new WorkerInitResponse()
                {
                    Result = new StatusResult()
                    {
                        Status = StatusResult.Types.Status.Success
                    }
                }
            };
        }

        internal StreamingMessage ProcessFunctionLoadRequest(StreamingMessage request)
        {
            FunctionLoadRequest functionLoadRequest = request.FunctionLoadRequest;

            // Assume success unless something bad happens
            StatusResult status = new StatusResult()
            {
                Status = StatusResult.Types.Status.Success
            };

            // Try to load the functions
            try
            {
                functionLoader.Load(functionLoadRequest.FunctionId, functionLoadRequest.Metadata);
            }
            catch (Exception e)
            {
                status.Status = StatusResult.Types.Status.Failure;
                status.Exception = e.ToRpcException();
            }

            return new StreamingMessage()
            {
                RequestId = request.RequestId,
                FunctionLoadResponse = new FunctionLoadResponse()
                {
                    FunctionId = functionLoadRequest.FunctionId,
                    Result = status
                }
            };
        }

        internal StreamingMessage ProcessInvocationRequest(StreamingMessage request)
        {
            InvocationRequest invocationRequest = request.InvocationRequest;

            // Set the RequestId and InvocationId for logging purposes
            logger.SetContext(request.RequestId, invocationRequest.InvocationId);

            // Load information about the function
            var functionInfo = functionLoader.GetInfo(invocationRequest.FunctionId);
            (string scriptPath, string entryPoint) = functionLoader.GetFunc(invocationRequest.FunctionId);

            // Bundle all TriggerMetadata into Hashtable to send down to PowerShell
            Hashtable triggerMetadata = new Hashtable();
            foreach (var dataItem in invocationRequest.TriggerMetadata)
            {
                triggerMetadata.Add(dataItem.Key, dataItem.Value.ToObject());
            }

            // Assume success unless something bad happens
            var status = new StatusResult() { Status = StatusResult.Types.Status.Success };
            var response = new StreamingMessage()
            {
                RequestId = request.RequestId,
                InvocationResponse = new InvocationResponse()
                {
                    InvocationId = invocationRequest.InvocationId,
                    Result = status
                }
            };

            // Invoke powershell logic and return hashtable of out binding data
            Hashtable result = null;
            try
            {
                result = powerShellManager
                    .InvokeFunctionAndSetGlobalReturn(scriptPath, entryPoint, triggerMetadata, invocationRequest.InputData)
                    .ReturnBindingHashtable(functionInfo.OutputBindings);
            }
            catch (Exception e)
            {
                status.Status = StatusResult.Types.Status.Failure;
                status.Exception = e.ToRpcException();
                return response;
            }

            // Set out binding data and return response to be sent back to host
            foreach (KeyValuePair<string, BindingInfo> binding in functionInfo.OutputBindings)
            {
                ParameterBinding paramBinding = new ParameterBinding()
                {
                    Name = binding.Key,
                    Data = result[binding.Key].ToTypedData()
                };

                response.InvocationResponse.OutputData.Add(paramBinding);

                // if one of the bindings is $return we need to also set the ReturnValue
                if(binding.Key == "$return")
                {
                    response.InvocationResponse.ReturnValue = paramBinding.Data;
                }
            }

            return response;
        }
    }
}
