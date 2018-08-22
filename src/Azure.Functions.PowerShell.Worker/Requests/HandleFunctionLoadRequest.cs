using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Extensions.Logging;

namespace  Microsoft.Azure.Functions.PowerShellWorker.Requests
{
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
            functionLoader.Load(functionLoadRequest.FunctionId, functionLoadRequest.Metadata);
            var response = new StreamingMessage()
            {
                RequestId = request.RequestId,
                FunctionLoadResponse = new FunctionLoadResponse()
                {
                    FunctionId = functionLoadRequest.FunctionId,
                    Result = new StatusResult()
                    {
                        Status = StatusResult.Types.Status.Success
                    }
                }
            };
            return response;
        }
    }
}