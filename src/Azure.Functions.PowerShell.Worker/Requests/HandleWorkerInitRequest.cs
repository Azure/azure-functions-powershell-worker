using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Extensions.Logging;

namespace  Microsoft.Azure.Functions.PowerShellWorker.Requests
{
    using System.Management.Automation;

    public class HandleWorkerInitRequest
    {
        public static StreamingMessage Invoke(
            PowerShell powershell,
            FunctionLoader functionLoader,
            StreamingMessage request,
            RpcLogger logger)
        {
            var response = new StreamingMessage()
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
            return response;
        }
    }
}