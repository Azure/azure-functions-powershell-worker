//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Text;

using Microsoft.Azure.Functions.PowerShellWorker.Messaging;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

namespace Microsoft.Azure.Functions.PowerShellWorker.Utility
{
    internal class RpcLogger : ILogger, IDisposable
    {
        private const string SystemLogPrefix = "LanguageWorkerConsoleLog";
        private readonly MessagingStream _msgStream;
        private readonly StringBuilder _systemLogMsg;
        private string _invocationId;
        private string _requestId;

        public RpcLogger(MessagingStream msgStream)
        {
            _msgStream = msgStream;
            _systemLogMsg = new StringBuilder();
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

        public async void Log(LogLevel logLevel, string message, Exception exception = null, bool isUserLog = false)
        {
            if (isUserLog)
            {
                // For user logs, we send them over Rpc with details about the invocation.
                var logMessage = new StreamingMessage
                {
                    RequestId = _requestId,
                    RpcLog = new RpcLog()
                    {
                        Exception = exception?.ToRpcException(),
                        InvocationId = _invocationId ?? "N/A",
                        Level = logLevel,
                        Message = message
                    }
                };

                await _msgStream.WriteAsync(logMessage);
            }
            else
            {
                // For system logs, we log to stdio with a prefix of LanguageWorkerConsoleLog.
                // These are picked up by the Functions Host
                _systemLogMsg.Append(SystemLogPrefix).AppendLine("System Log: {");
                if (!string.IsNullOrEmpty(_requestId))
                {
                    _systemLogMsg.AppendLine($"  Request-Id: {_requestId}");
                }
                if (!string.IsNullOrEmpty(_invocationId))
                {
                    _systemLogMsg.AppendLine($"  Invocation-Id: {_invocationId}");
                }
                _systemLogMsg.AppendLine($"  Log-Message: {message}").AppendLine("}");

                Console.WriteLine(_systemLogMsg.ToString());
                _systemLogMsg.Clear();
            }
        }
    }
}
