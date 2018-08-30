//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;

using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace Microsoft.Azure.Functions.PowerShellWorker.Requests
{
    internal static class HandleInvocationRequest
    {
        public static StreamingMessage Invoke(
            PowerShellManager powerShellManager,
            FunctionLoader functionLoader,
            StreamingMessage request,
            RpcLogger logger)
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
                result = powerShellManager.InvokeFunction(
                    scriptPath,
                    entryPoint,
                    triggerMetadata,
                    invocationRequest.InputData);
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
                // TODO: How do we want to handle when a binding is not set?
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
