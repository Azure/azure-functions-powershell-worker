//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

using Microsoft.Azure.Functions.PowerShellWorker.Messaging;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Functions.PowerShellWorker.Utility
{
    internal class RpcLogger : ILogger
    {
        FunctionMessagingClient _client;
        string _invocationId = "";
        string _requestId = "";

        public RpcLogger(FunctionMessagingClient client)
        {
            _client = client;
        }

        public IDisposable BeginScope<TState>(TState state) =>
            throw new NotImplementedException();

        public static RpcLog.Types.Level ConvertLogLevel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Critical:
                    return RpcLog.Types.Level.Critical;
                case LogLevel.Debug:
                    return RpcLog.Types.Level.Debug;
                case LogLevel.Error:
                    return RpcLog.Types.Level.Error;
                case LogLevel.Information:
                    return RpcLog.Types.Level.Information;
                case LogLevel.Trace:
                    return RpcLog.Types.Level.Trace;
                case LogLevel.Warning:
                    return RpcLog.Types.Level.Warning;
                default:
                    return RpcLog.Types.Level.None;
            }
        }

        public bool IsEnabled(LogLevel logLevel) =>
            throw new NotImplementedException();

        public async void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (_client != null)
            {
                var logMessage = new StreamingMessage
                {
                    RequestId = _requestId,
                    RpcLog = new RpcLog()
                    {
                        Exception = exception?.ToRpcException(),
                        InvocationId = _invocationId,
                        Level = ConvertLogLevel(logLevel),
                        Message = formatter(state, exception)
                    }
                };

                await _client.WriteAsync(logMessage);
            }
        }

        public void SetContext(string requestId, string invocationId)
        {
            _requestId = requestId;
            _invocationId = invocationId;
        }
    }
}
