using System;
using Azure.Functions.PowerShell.Worker.Messaging;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Extensions.Logging;
using static Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types;

namespace Microsoft.Azure.Functions.PowerShellWorker.Utility
{
    public class RpcLogger : ILogger
    {
        private FunctionMessagingClient _Client;
        private string _invocationId = "";
        private string _requestId = "";

        public RpcLogger(FunctionMessagingClient client)
        {
            _Client = client;
        }

        public void SetContext(string requestId, string invocationId)
        {
            _requestId = requestId;
            _invocationId = invocationId;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            throw new NotImplementedException();
        }

        public async void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (_Client != null)
            {
                var logMessage = new StreamingMessage
                {
                    RequestId = _requestId,
                    RpcLog = new RpcLog()
                    {
                        Exception = exception == null ? null : exception.ToRpcException(),
                        InvocationId = _invocationId,
                        Level = ConvertLogLevel(logLevel),
                        Message = formatter(state, exception)
                    }
                };

                await _Client.WriteAsync(logMessage);
            }
        }

        public static Level ConvertLogLevel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Critical:
                    return Level.Critical;
                case LogLevel.Debug:
                    return Level.Debug;
                case LogLevel.Error:
                    return Level.Error;
                case LogLevel.Information:
                    return Level.Information;
                case LogLevel.Trace:
                    return Level.Trace;
                case LogLevel.Warning:
                    return Level.Warning;
                default:
                    return Level.None;
            }
        }
    }
}