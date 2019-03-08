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
using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

namespace  Microsoft.Azure.Functions.PowerShellWorker
{
    internal class RequestProcessor
    {
        private readonly FunctionLoader _functionLoader;
        private readonly MessagingStream _msgStream;
        private readonly PowerShellManagerPool _powershellPool;

        // Indicate whether the FunctionApp has been initialized.
        private bool _isFunctionAppInitialized;

        private Dictionary<StreamingMessage.ContentOneofCase, Func<StreamingMessage, StreamingMessage>> _requestHandlers =
            new Dictionary<StreamingMessage.ContentOneofCase, Func<StreamingMessage, StreamingMessage>>();

        internal RequestProcessor(MessagingStream msgStream)
        {
            _msgStream = msgStream;
            _powershellPool = new PowerShellManagerPool(msgStream);
            _functionLoader = new FunctionLoader();
            
            // Host sends capabilities/init data to worker
            _requestHandlers.Add(StreamingMessage.ContentOneofCase.WorkerInitRequest, ProcessWorkerInitRequest);

            // Host sends terminate message to worker.
            // Worker terminates if it can, otherwise host terminates after a grace period
            _requestHandlers.Add(StreamingMessage.ContentOneofCase.WorkerTerminate, ProcessWorkerTerminateRequest);

            // Add any worker relevant status to response
            _requestHandlers.Add(StreamingMessage.ContentOneofCase.WorkerStatusRequest, ProcessWorkerStatusRequest);

            // On file change event, host sends notification to worker
            _requestHandlers.Add(StreamingMessage.ContentOneofCase.FileChangeEventRequest, ProcessFileChangeEventRequest);

            // Host sends required metadata to worker to load function
            _requestHandlers.Add(StreamingMessage.ContentOneofCase.FunctionLoadRequest, ProcessFunctionLoadRequest);
            
            // Host requests a given invocation
            _requestHandlers.Add(StreamingMessage.ContentOneofCase.InvocationRequest, ProcessInvocationRequest);

            // Host sends cancel message to attempt to cancel an invocation. 
            // If an invocation is cancelled, host will receive an invocation response with status cancelled.
            _requestHandlers.Add(StreamingMessage.ContentOneofCase.InvocationCancel, ProcessInvocationCancelRequest);

            _requestHandlers.Add(StreamingMessage.ContentOneofCase.FunctionEnvironmentReloadRequest, ProcessFunctionEnvironmentReloadRequest);
        }

        internal async Task ProcessRequestLoop()
        {
            var logger = new RpcLogger(_msgStream);

            StreamingMessage request, response;
            while (await _msgStream.MoveNext())
            {
                request = _msgStream.GetCurrentMessage();

                if (_requestHandlers.TryGetValue(request.ContentCase, out Func<StreamingMessage, StreamingMessage> requestFunc))
                {
                    response = requestFunc(request);
                }
                else
                {
                    logger.Log(LogLevel.Error, string.Format(PowerShellWorkerStrings.UnsupportedMessage, request.ContentCase));
                    continue;
                }

                if (response != null)
                {
                    _msgStream.Write(response);
                }
            }
        }

        internal StreamingMessage ProcessWorkerInitRequest(StreamingMessage request)
        {
            StreamingMessage response = NewStreamingMessageTemplate(
                request.RequestId,
                StreamingMessage.ContentOneofCase.WorkerInitResponse,
                out StatusResult status);

            return response;
        }

        internal StreamingMessage ProcessWorkerTerminateRequest(StreamingMessage request)
        {
            return null;
        }

        internal StreamingMessage ProcessWorkerStatusRequest(StreamingMessage request)
        {
            // WorkerStatusResponse type says that it is not used but this will create an empty one anyway to return to the host
            StreamingMessage response = NewStreamingMessageTemplate(
                request.RequestId,
                StreamingMessage.ContentOneofCase.WorkerStatusResponse,
                out StatusResult status);

            return response;
        }

        internal StreamingMessage ProcessFileChangeEventRequest(StreamingMessage request)
        {
            return null;
        }

        /// <summary>
        /// Method to process a FunctionLoadRequest.
        /// FunctionLoadRequest should be processed sequentially. There is no point to process FunctionLoadRequest
        /// concurrently as a FunctionApp doesn't include a lot functions in general. Having this step sequential
        /// will make the Runspace-level initialization easier and more predictable.
        /// </summary>
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
                // Ideally, the initialization should happen when processing 'WorkerInitRequest', however, the 'WorkerInitRequest'
                // message doesn't provide the file path of the FunctionApp. That information is not available until the first
                // 'FunctionLoadRequest' comes in. Therefore, we run initialization here.
                if (!_isFunctionAppInitialized)
                {
                    FunctionLoader.SetupWellKnownPaths(functionLoadRequest);
                    _powershellPool.Initialize(request.RequestId);
                    _isFunctionAppInitialized = true;
                }

                // Load the metadata of the function.
                _functionLoader.LoadFunction(functionLoadRequest);
            }
            catch (Exception e)
            {
                status.Status = StatusResult.Types.Status.Failure;
                status.Exception = e.ToRpcException();
            }

            return response;
        }

        /// <summary>
        /// Method to process a InvocationRequest.
        /// This method checks out a worker from the pool, and then starts the actual invocation in a threadpool thread.
        /// </summary>
        internal StreamingMessage ProcessInvocationRequest(StreamingMessage request)
        {
            AzFunctionInfo functionInfo = null;
            PowerShellManager psManager = null;

            try
            {
                functionInfo = _functionLoader.GetFunctionInfo(request.InvocationRequest.FunctionId);
                psManager = _powershellPool.CheckoutIdleWorker(request, functionInfo);
                Task.Run(() => ProcessInvocationRequestImpl(request, functionInfo, psManager));
            }
            catch (Exception e)
            {
                _powershellPool.ReclaimUsedWorker(psManager);

                StreamingMessage response = NewStreamingMessageTemplate(
                    request.RequestId,
                    StreamingMessage.ContentOneofCase.InvocationResponse,
                    out StatusResult status);

                response.InvocationResponse.InvocationId = request.InvocationRequest.InvocationId;
                status.Status = StatusResult.Types.Status.Failure;
                status.Exception = e.ToRpcException();

                return response;
            }

            return null;
        }

        /// <summary>
        /// Implementation method to actual invoke the corresponding function.
        /// InvocationRequest messages are processed in parallel when there are multiple PowerShellManager instances in the pool.
        /// </summary>
        private void ProcessInvocationRequestImpl(StreamingMessage request, AzFunctionInfo functionInfo, PowerShellManager psManager)
        {
            InvocationRequest invocationRequest = request.InvocationRequest;
            StreamingMessage response = NewStreamingMessageTemplate(
                request.RequestId,
                StreamingMessage.ContentOneofCase.InvocationResponse,
                out StatusResult status);
            response.InvocationResponse.InvocationId = invocationRequest.InvocationId;

            try
            {
                // Invoke the function and return a hashtable of out binding data
                Hashtable results = functionInfo.Type == AzFunctionType.OrchestrationFunction
                    ? InvokeOrchestrationFunction(psManager, functionInfo, invocationRequest)
                    : InvokeSingleActivityFunction(psManager, functionInfo, invocationRequest);

                BindOutputFromResult(response.InvocationResponse, functionInfo, results);
            }
            catch (Exception e)
            {
                status.Status = StatusResult.Types.Status.Failure;
                status.Exception = e.ToRpcException();
            }
            finally
            {
                _powershellPool.ReclaimUsedWorker(psManager);
            }

            _msgStream.Write(response);
        }

        internal StreamingMessage ProcessInvocationCancelRequest(StreamingMessage request)
        {
            return null;
        }

        internal StreamingMessage ProcessFunctionEnvironmentReloadRequest(StreamingMessage request)
        {
            StreamingMessage response = NewStreamingMessageTemplate(
                request.RequestId,
                StreamingMessage.ContentOneofCase.FunctionEnvironmentReloadResponse,
                out StatusResult status);

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
                case StreamingMessage.ContentOneofCase.WorkerStatusResponse:
                    response.WorkerStatusResponse = new WorkerStatusResponse();
                    break;
                case StreamingMessage.ContentOneofCase.FunctionLoadResponse:
                    response.FunctionLoadResponse = new FunctionLoadResponse() { Result = status };
                    break;
                case StreamingMessage.ContentOneofCase.InvocationResponse:
                    response.InvocationResponse = new InvocationResponse() { Result = status };
                    break;
                case StreamingMessage.ContentOneofCase.FunctionEnvironmentReloadResponse:
                    response.FunctionEnvironmentReloadResponse = new FunctionEnvironmentReloadResponse() { Result = status };
                    break;
                default:
                    throw new InvalidOperationException("Unreachable code.");
            }

            return response;
        }

        /// <summary>
        /// Invoke an orchestration function.
        /// </summary>
        private Hashtable InvokeOrchestrationFunction(PowerShellManager psManager, AzFunctionInfo functionInfo, InvocationRequest invocationRequest)
        {
            throw new NotImplementedException(PowerShellWorkerStrings.DurableFunctionNotSupported);
        }

        /// <summary>
        /// Invoke a regular function or an activity function.
        /// </summary>
        private Hashtable InvokeSingleActivityFunction(PowerShellManager psManager, AzFunctionInfo functionInfo, InvocationRequest invocationRequest)
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

            return psManager.InvokeFunction(functionInfo, triggerMetadata, invocationRequest.InputData);
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
                    foreach (KeyValuePair<string, ReadOnlyBindingInfo> binding in functionInfo.OutputBindings)
                    {
                        string outBindingName = binding.Key;
                        ReadOnlyBindingInfo bindingInfo = binding.Value;

                        object outValue = results[outBindingName];
                        object transformedValue = Utils.TransformOutBindingValueAsNeeded(outBindingName, bindingInfo, outValue);
                        TypedData dataToUse = transformedValue.ToTypedData();

                        // if one of the bindings is '$return' we need to set the ReturnValue
                        if(string.Equals(outBindingName, AzFunctionInfo.DollarReturn, StringComparison.OrdinalIgnoreCase))
                        {
                            response.ReturnValue = dataToUse;
                            continue;
                        }

                        ParameterBinding paramBinding = new ParameterBinding()
                        {
                            Name = outBindingName,
                            Data = dataToUse
                        };

                        response.OutputData.Add(paramBinding);
                    }
                    break;

                case AzFunctionType.OrchestrationFunction:
                case AzFunctionType.ActivityFunction:
                    response.ReturnValue = results[AzFunctionInfo.DollarReturn].ToTypedData();
                    break;

                default:
                    throw new InvalidOperationException("Unreachable code.");
            }
        }

        #endregion
    }
}
