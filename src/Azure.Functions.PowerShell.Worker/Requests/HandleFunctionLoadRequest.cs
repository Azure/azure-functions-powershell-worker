//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace  Microsoft.Azure.Functions.PowerShellWorker.Requests
{
    using System.Management.Automation;

    internal static class HandleFunctionLoadRequest
    {
        public static StreamingMessage Invoke(
            PowerShell powershell,
            FunctionLoader functionLoader,
            StreamingMessage request,
            RpcLogger logger)
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
    }
}