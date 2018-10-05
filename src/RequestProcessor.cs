//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

using Newtonsoft.Json;
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
            StreamingMessage response = NewStreamingMessageTemplate(
                request.RequestId,
                StreamingMessage.ContentOneofCase.WorkerInitResponse,
                out StatusResult status);

            try
            {
                _powerShellManager.InitializeRunspace();
            }
            catch (Exception e)
            {
                status.Status = StatusResult.Types.Status.Failure;
                status.Exception = e.ToRpcException();
            }

            return response;
        }

        internal StreamingMessage ProcessFunctionLoadRequest(StreamingMessage request)
        {
            FunctionLoadRequest functionLoadRequest = request.FunctionLoadRequest;

            StreamingMessage response = NewStreamingMessageTemplate(
                request.RequestId,
                StreamingMessage.ContentOneofCase.FunctionLoadResponse,
                out StatusResult status);
            response.FunctionLoadResponse.FunctionId = functionLoadRequest.FunctionId;

            try
            {
                // Try loading the metadata of the function
                _functionLoader.Load(functionLoadRequest);
            }
            catch (Exception e)
            {
                status.Status = StatusResult.Types.Status.Failure;
                status.Exception = e.ToRpcException();
            }

            return response;
        }

        internal StreamingMessage ProcessInvocationRequest(StreamingMessage request)
        {
            InvocationRequest invocationRequest = request.InvocationRequest;

            StreamingMessage response = NewStreamingMessageTemplate(
                request.RequestId,
                StreamingMessage.ContentOneofCase.InvocationResponse,
                out StatusResult status);
            response.InvocationResponse.InvocationId = invocationRequest.InvocationId;

            // Invoke powershell logic and return hashtable of out binding data
            try
            {
                // Load information about the function
                var functionInfo = _functionLoader.GetFunctionInfo(invocationRequest.FunctionId);

                Hashtable results = functionInfo.Type == AzFunctionType.OrchestrationFunction
                    ? InvokeOrchestrationFunction(functionInfo, invocationRequest)
                    : InvokeSingleActivityFunction(functionInfo, invocationRequest);

                BindOutputFromResult(response.InvocationResponse, functionInfo, results);
            }
            catch (Exception e)
            {
                status.Status = StatusResult.Types.Status.Failure;
                status.Exception = e.ToRpcException();
            }

            return response;
        }

        #region Helper_Methods

        /// <summary>
        /// Create an object of 'StreamingMessage' as a template, for further update.
        /// </summary>
        private StreamingMessage NewStreamingMessageTemplate(string requestId, StreamingMessage.ContentOneofCase msgType, out StatusResult status)
        {
            // Assume success. The state of the status object can be changed in the caller.
            status = new StatusResult() { Status = StatusResult.Types.Status.Success };
            var response = new StreamingMessage() { RequestId = requestId };

            switch (msgType)
            {
                case StreamingMessage.ContentOneofCase.WorkerInitResponse:
                    response.WorkerInitResponse = new WorkerInitResponse() { Result = status };
                    break;
                case StreamingMessage.ContentOneofCase.FunctionLoadResponse:
                    response.FunctionLoadResponse = new FunctionLoadResponse() { Result = status };
                    break;
                case StreamingMessage.ContentOneofCase.InvocationResponse:
                    response.InvocationResponse = new InvocationResponse() { Result = status };
                    break;
                default:
                    throw new InvalidOperationException("Unreachable code.");
            }

            return response;
        }

        /// <summary>
        /// Invoke an orchestration function.
        /// </summary>
        private Hashtable InvokeOrchestrationFunction(AzFunctionInfo functionInfo, InvocationRequest invocationRequest)
        {
            // Quote from https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-bindings:
            //
            // "Orchestrator functions should never use any input or output bindings other than the orchestration trigger binding.
            //  Doing so has the potential to cause problems with the Durable Task extension because those bindings may not obey the single-threading and I/O rules."
            //
            // Therefore, it's by design that 'InputData' contains only one item, which is the metadata of the orchestration context.

            ParameterBinding context = invocationRequest.InputData[0];
            var durableFuncContext = JsonConvert.DeserializeObject<OrchestrationContext>(context.Data.String);
            return _powerShellManager.InvokeFunction(functionInfo, context.Name, durableFuncContext);
        }

        /// <summary>
        /// Invoke a regular function or an activity function.
        /// </summary>
        private Hashtable InvokeSingleActivityFunction(AzFunctionInfo functionInfo, InvocationRequest invocationRequest)
        {
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

            return _powerShellManager.InvokeFunction(
                functionInfo,
                triggerMetadata,
                invocationRequest.InputData);
        }

        /// <summary>
        /// Set the 'ReturnValue' and 'OutputData' based on the invocation results appropriately.
        /// </summary>
        private void BindOutputFromResult(InvocationResponse response, AzFunctionInfo functionInfo, Hashtable results)
        {
            switch (functionInfo.Type)
            {
                case AzFunctionType.RegularFunction:
                    // Set out binding data and return response to be sent back to host
                    foreach (KeyValuePair<string, BindingInfo> binding in functionInfo.OutputBindings)
                    {
                        // if one of the bindings is '$return' we need to set the ReturnValue
                        string outBindingName = binding.Key;
                        if(string.Equals(outBindingName, AzFunctionInfo.DollarReturn, StringComparison.OrdinalIgnoreCase))
                        {
                            response.ReturnValue = results[outBindingName].ToTypedData(_powerShellManager);
                            continue;
                        }

                        ParameterBinding paramBinding = new ParameterBinding()
                        {
                            Name = outBindingName,
                            Data = results[outBindingName].ToTypedData(_powerShellManager)
                        };

                        response.OutputData.Add(paramBinding);
                    }
                    break;

                case AzFunctionType.OrchestrationFunction:
                case AzFunctionType.ActivityFunction:
                    response.ReturnValue = results[AzFunctionInfo.DollarReturn].ToTypedData(_powerShellManager);
                    break;

                default:
                    throw new InvalidOperationException("Unreachable code.");
            }
        }

        #endregion
    }
}
