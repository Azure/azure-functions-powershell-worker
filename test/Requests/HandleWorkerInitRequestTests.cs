//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Azure.Functions.PowerShellWorker;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Xunit;

namespace Microsoft.Azure.Functions.PowerShellWorker.Test
{
    public class ProcessWorkerInitRequestTests
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

            var requestProcessor = new RequestProcessor(null);
            StreamingMessage result = requestProcessor.ProcessWorkerInitRequest(
                new StreamingMessage()
                {
                    RequestId = requestId
                }
            );

            Assert.Equal(requestId, result.RequestId);
            Assert.Equal(status, result.WorkerInitResponse.Result.Status);
        }
    }
}
