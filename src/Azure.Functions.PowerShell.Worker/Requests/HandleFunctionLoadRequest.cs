using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Extensions.Logging;

namespace  Microsoft.Azure.Functions.PowerShellWorker.Requests
{
    using System;
    using System.Management.Automation;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    public class HandleFunctionLoadRequest
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