//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Azure.Functions.PowerShellWorker.Messaging;
using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace  Microsoft.Azure.Functions.PowerShellWorker
{
    internal class RequestProcessor
    {
        private readonly FunctionLoader _functionLoader;
        private readonly RpcLogger _logger;
        private readonly MessagingStream _msgStream;
        private readonly PowerShellManager _powerShellManager;

        internal RequestProcessor(MessagingStream msgStream)
        {
            _msgStream = msgStream;
            _logger = new RpcLogger(msgStream);
            _powerShellManager = new PowerShellManager(_logger);
            _functionLoader = new FunctionLoader();
        }

        internal async Task ProcessRequestLoop()
        {
            using (_msgStream)
            {
                StreamingMessage request, response;
                while (await _msgStream.MoveNext())
                {
                    request = _msgStream.GetCurrentMessage();
                    
                    using (_logger.BeginScope(request.RequestId, request.InvocationRequest?.InvocationId))
                    {
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
                    }
                    await _msgStream.WriteAsync(response);
                }
            }
        }

        internal StreamingMessage ProcessWorkerInitRequest(StreamingMessage request)
        {
            StatusResult status = new StatusResult()
            {
                Status = StatusResult.Types.Status.Success
            };

            try
            {
                _powerShellManager.InitializeRunspace();
            }
            catch (Exception e)
            {
                status.Status = StatusResult.Types.Status.Failure;
                status.Exception = e.ToRpcException();
            }

            return new StreamingMessage()
            {
                RequestId = request.RequestId,
                WorkerInitResponse = new WorkerInitResponse()
                {
                    Result = status
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
                _functionLoader.Load(functionLoadRequest);
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
            try
            {
                // Load information about the function
                var functionInfo = _functionLoader.GetFunctionInfo(invocationRequest.FunctionId);

                // Bundle all TriggerMetadata into Hashtable to send down to PowerShell
                var triggerMetadata = new Hashtable(StringComparer.OrdinalIgnoreCase);
                foreach (var dataItem in invocationRequest.TriggerMetadata)
                {
                    // MapField<K, V> is case-sensitive, but powershell is case-insensitive,
                    // so for keys differ only in casing, the first wins.
                    if (!triggerMetadata.ContainsKey(dataItem.Key))
                    {
                        triggerMetadata.Add(dataItem.Key, dataItem.Value.ToObject());
                    }
                }

                // Set the RequestId and InvocationId for logging purposes
                Hashtable result = null;
                result = _powerShellManager.InvokeFunction(
                    functionInfo.ScriptPath,
                    functionInfo.EntryPoint,
                    triggerMetadata,
                    invocationRequest.InputData);

                // Set out binding data and return response to be sent back to host
                foreach (KeyValuePair<string, BindingInfo> binding in functionInfo.OutputBindings)
                {
                    // if one of the bindings is '$return' we need to set the ReturnValue
                    if(string.Equals(binding.Key, "$return", StringComparison.OrdinalIgnoreCase))
                    {
                        response.InvocationResponse.ReturnValue = result[binding.Key].ToTypedData();
                        continue;
                    }

                    ParameterBinding paramBinding = new ParameterBinding()
                    {
                        Name = binding.Key,
                        Data = result[binding.Key].ToTypedData()
                    };

                    response.InvocationResponse.OutputData.Add(paramBinding);
                }
            }
            catch (Exception e)
            {
                status.Status = StatusResult.Types.Status.Failure;
                status.Exception = e.ToRpcException();
            }

            return response;
        }
    }
}
