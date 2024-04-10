using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using System.Linq;
using System;

namespace Microsoft.Azure.Functions.PowerShellWorker.OpenTelemetry
{
    using PowerShell = System.Management.Automation.PowerShell;
    using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

    internal class OpenTelemetryServices : IOpenTelemetryServices
    {
        private const string StartOpenTelemetryInvocationCmdlet = "Start-OpenTelemetryInvocationInternal";
        private const string StopOpenTelemetryInvocationCmdlet = "Stop-OpenTelemetryInvocationInternal";
        private const string GetFunctionsLogHandlerCmdlet = "Get-FunctionsLogHandlerInternal";

        private readonly ILogger _logger;
        public readonly PowerShell _pwsh;

        public OpenTelemetryServices(ILogger logger, PowerShell pwsh)
        {
            _logger = logger;
            _pwsh = pwsh;
        }

        public bool? IsModuleLoaded()
        {
            return PowerShellModuleDetector.IsPowerShellModuleLoaded(_pwsh, _logger, Utils.OpenTelemetrySdkName);
        }

        public void AddStartOpenTelemetryInvocationCommand(OpenTelemetryInvocationContext otelContext)
        {
            if (!otelContext.isValid())
            {
                _logger.Log(false, LogLevel.Warning, PowerShellWorkerStrings.InvalidOpenTelemetryContext);
            }

            _pwsh.AddCommand(StartOpenTelemetryInvocationCmdlet)
                .AddParameter("InvocationId", otelContext.InvocationId)
                .AddParameter("TraceParent", otelContext.TraceParent)
                .AddParameter("TraceState", otelContext.TraceState);
        }

        public void StopOpenTelemetryInvocation(OpenTelemetryInvocationContext otelContext, bool _testing)
        {
            _pwsh.AddCommand(StopOpenTelemetryInvocationCmdlet)
                .AddParameter("InvocationId", otelContext.InvocationId);

            if (!_testing)
            {
                _pwsh.InvokeAndClearCommands();
            }
        }

        public void StartFunctionsLoggingListener(bool testing)
        {
            _pwsh.AddCommand(GetFunctionsLogHandlerCmdlet);

            if (testing)
            {
                return;
            }    

            var eventHandlers =
                _pwsh.InvokeAndClearCommands<Action<string, string, Exception>>();

            if (eventHandlers.Any())
            {
                if (_logger is RpcLogger rpcLogger)
                {
                    rpcLogger.outputLogHandler.Subscribe(eventHandlers.First());
                }
            }
        }
    }
}
