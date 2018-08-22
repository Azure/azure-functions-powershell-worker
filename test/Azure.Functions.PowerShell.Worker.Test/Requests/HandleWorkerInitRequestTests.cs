using System;
using Microsoft.Azure.Functions.PowerShellWorker.Requests;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Xunit;

namespace Azure.Functions.PowerShell.Worker.Test
{
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    public class HandleWorkerInitRequestTests
    {
        [Fact]
        public void HandleWorkerInitRequestSuccess()
        {
            var requestId = "testRequest";
            var status = StatusResult.Types.Status.Success;
            var expectedResponse = new StreamingMessage()
            {
                RequestId = requestId,
                WorkerInitResponse = new WorkerInitResponse()
                {
                    Result = new StatusResult()
                    {
                        Status = status
                    }
                }
            };

            StreamingMessage result = HandleWorkerInitRequest.Invoke(
                null,
                null,
                new StreamingMessage()
                {
                    RequestId = requestId
                },
                new RpcLogger(null)
            );

            Assert.Equal(requestId, result.RequestId);
            Assert.Equal(status, result.WorkerInitResponse.Result.Status);
        }
    }
}
