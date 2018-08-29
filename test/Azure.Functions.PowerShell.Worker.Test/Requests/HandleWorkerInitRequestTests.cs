//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Azure.Functions.PowerShellWorker.Requests;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Xunit;

namespace Azure.Functions.PowerShell.Worker.Test
{
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
