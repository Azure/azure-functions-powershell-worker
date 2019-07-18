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
    internal class RpcLogger : ILogger
    {
        private const string SystemLogPrefix = "LanguageWorkerConsoleLog";
        private readonly MessagingStream _msgStream;
        private string _invocationId;
        private string _requestId;

        internal RpcLogger(MessagingStream msgStream)
        {
            _msgStream = msgStream;
        }

        public void SetContext(string requestId, string invocationId)
        {
            _requestId = requestId;
            _invocationId = invocationId;
        }

        public void ResetContext()
        {
            _requestId = null;
            _invocationId = null;
        }

        public void Log(LogLevel logLevel, string message, Exception exception = null, bool isUserLog = false)
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

                _msgStream.Write(logMessage);
            }
            else
            {
                WriteSystemLog(logLevel, message, _requestId, _invocationId);
            }
        }

        private static void WriteSystemLog(LogLevel logLevel, string message, string requestId, string invocationId)
        {
            // For system logs, we log to stdio with a prefix of LanguageWorkerConsoleLog.
            // These are picked up by the Functions Host
            var stringBuilder = new StringBuilder(SystemLogPrefix);

            stringBuilder.Append("System Log: {");
            if (!string.IsNullOrEmpty(requestId))
            {
                stringBuilder.Append($" Request-Id: {requestId};");
            }
            if (!string.IsNullOrEmpty(invocationId))
            {
                stringBuilder.Append($" Invocation-Id: {invocationId};");
            }
            stringBuilder.Append($" Log-Level: {logLevel};");
            stringBuilder.Append($" Log-Message: {message}");
            stringBuilder.AppendLine(" }");

            Console.WriteLine(stringBuilder.ToString());
        }

        internal static void WriteSystemLog(LogLevel logLevel, string message)
        {
            WriteSystemLog(logLevel, message, requestId: null, invocationId: null);
        }
    }
}
