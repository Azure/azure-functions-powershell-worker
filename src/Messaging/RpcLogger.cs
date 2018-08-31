//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

using Microsoft.Azure.Functions.PowerShellWorker.Messaging;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

namespace Microsoft.Azure.Functions.PowerShellWorker.Utility
{
    internal class RpcLogger : IDisposable
    {
        private MessagingStream _msgStream;
        private string _invocationId;
        private string _requestId;

        public RpcLogger(MessagingStream msgStream)
        {
            _msgStream = msgStream;
        }

        public IDisposable BeginScope(string requestId, string invocationId)
        {
            _requestId = requestId;
            _invocationId = invocationId;
            return this;
        }

        public void Dispose()
        {
            _requestId = null;
            _invocationId = null;
        }

        public async void Log(LogLevel logLevel, string message, Exception exception = null)
        {
            var logMessage = new StreamingMessage
            {
                RequestId = _requestId,
                RpcLog = new RpcLog()
                {
                    Exception = exception?.ToRpcException(),
                    InvocationId = _invocationId,
                    Level = logLevel,
                    Message = message
                }
            };

            await _msgStream.WriteAsync(logMessage);
        }
    }
}
