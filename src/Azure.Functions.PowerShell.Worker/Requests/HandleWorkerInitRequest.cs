//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

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
            return new StreamingMessage()
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
        }
    }
}