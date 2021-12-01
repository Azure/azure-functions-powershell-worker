//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Azure.WebJobs.Script.Grpc.Messages;


namespace Microsoft.Azure.Functions.PowerShellWorker
{
    /// <summary>
    /// Custom RetryContext constructed from the RpcTraceContext member received from the host.
    /// </summary>
    internal class RetryContext
    {
        public RetryContext(int retryCount, int maxRetryCount, RpcException exception)
        {
            RetryCount = retryCount;
            MaxRetryCount = maxRetryCount;
            Exception = exception;
        }

        public int RetryCount { get; }

        public int MaxRetryCount { get; }

        public RpcException Exception { get; }
    }
}
