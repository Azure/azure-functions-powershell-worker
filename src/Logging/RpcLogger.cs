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
        private readonly StringBuilder _systemLogMsg;
        private string _invocationId;
        private string _requestId;

        internal RpcLogger(MessagingStream msgStream)
        {
            _msgStream = msgStream;
            _systemLogMsg = new StringBuilder();
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
                WriteSystemLog(message, _systemLogMsg, _requestId, _invocationId);
            }
        }

        private static void WriteSystemLog(string message, StringBuilder stringBuilder, string requestId, string invocationId)
        {
            stringBuilder = stringBuilder ?? new StringBuilder();

            // For system logs, we log to stdio with a prefix of LanguageWorkerConsoleLog.
            // These are picked up by the Functions Host
            stringBuilder.Append(SystemLogPrefix).AppendLine("System Log: {");
            if (!string.IsNullOrEmpty(requestId))
            {
                stringBuilder.AppendLine($"  Request-Id: {requestId}");
            }
            if (!string.IsNullOrEmpty(invocationId))
            {
                stringBuilder.AppendLine($"  Invocation-Id: {invocationId}");
            }
            stringBuilder.AppendLine($"  Log-Message: {message}").AppendLine("}");

            Console.WriteLine(stringBuilder.ToString());
            stringBuilder.Clear();
        }

        internal static void WriteSystemLog(string message)
        {
            WriteSystemLog(message, stringBuilder: null, requestId: null, invocationId: null);
        }
    }
}
