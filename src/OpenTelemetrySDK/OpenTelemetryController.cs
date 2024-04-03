using System;
using System.Linq;
using System.Management.Automation;
using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

namespace Microsoft.Azure.Functions.PowerShellWorker.OpenTelemetrySDK
{
    using PowerShell = System.Management.Automation.PowerShell;

    internal class OpenTelemetryController
    {
        private readonly ILogger _logger;
        private PowerShell _pwsh { get; set; }

        private const string StartOpenTelemetryInvocationCmdlet = "Start-OpenTelemetryInvocation";
        private const string StopOpenTelemetryInvocationCmdlet = "Stop-OpenTelemetryInvocation";
        private const string GetFunctionsLogHandlerCmdlet = "Get-FunctionsLogHandler";

        private const string OTelEnvironmentVariableName = "OTEL_FUNCTIONS_WORKER_ENABLED";

        private static bool? _isOpenTelemetryCapable;
        private static bool? _isOpenTelemetryModuleLoaded;
        private static bool? _isOpenTelmetryEnvironmentEnabled;

        public OpenTelemetryController(ILogger logger, PowerShell pwsh)
        {
            _logger = logger;
            _pwsh = pwsh;
        }

        public bool isOpenTelemetryCapable()
        {
            if (_isOpenTelemetryCapable.HasValue)
            {
                return _isOpenTelemetryCapable.Value;
            }

            _isOpenTelemetryCapable = isOpenTelemetryEnvironmentEnabled() && isOpenTelemetryModuleLoaded();

            return _isOpenTelemetryCapable.Value;
        }

        public static bool isOpenTelemetryEnvironmentEnabled()
        {
            if (_isOpenTelmetryEnvironmentEnabled.HasValue)
            {
                return _isOpenTelmetryEnvironmentEnabled.Value;
            }

            string isOpenTelemetryWorkerLogEnabled = Environment.GetEnvironmentVariable(OTelEnvironmentVariableName);
            _isOpenTelmetryEnvironmentEnabled = !string.IsNullOrEmpty(isOpenTelemetryWorkerLogEnabled);

            return _isOpenTelmetryEnvironmentEnabled.Value;
        }

        public bool isOpenTelemetryModuleLoaded()
        {
            if (_isOpenTelemetryModuleLoaded.HasValue)
            {
                return _isOpenTelemetryModuleLoaded.Value;
            }

            // Search for the OpenTelemetry SDK in the current session
            var matchingModules = _pwsh.AddCommand(Utils.GetModuleCmdletInfo)
                .AddParameter("FullyQualifiedName", Utils.OpenTelemetrySdkName)
                .InvokeAndClearCommands<PSModuleInfo>();

            // If we get at least one result, we know the OpenTelemetry SDK was imported
            var numCandidates = matchingModules.Count();
            var isModuleInCurrentSession = numCandidates > 0;

            if (isModuleInCurrentSession)
            {
                var candidatesInfo = matchingModules.Select(module => string.Format(
                    PowerShellWorkerStrings.FoundOpenTelemetrySdkInSession, module.Name, module.Version, module.Path));
                var otelSDKModuleInfo = string.Join('\n', candidatesInfo);

                if (numCandidates > 1)
                {
                    // If there's more than 1 result, there may be runtime conflicts
                    // warn user of potential conflicts
                    _logger.Log(isUserOnlyLog: false, LogLevel.Warning, String.Format(
                        PowerShellWorkerStrings.MultipleExternalSDKsInSession,
                        numCandidates, Utils.OpenTelemetrySdkName, otelSDKModuleInfo));
                }
                else
                {
                    // a single SDK is in session. Report its metadata
                    _logger.Log(isUserOnlyLog: false, LogLevel.Trace, otelSDKModuleInfo);
                }
            }

            _isOpenTelemetryModuleLoaded = isModuleInCurrentSession;


            return _isOpenTelemetryModuleLoaded.Value;
        }

        public static void ResetOpenTelemetryModuleStatus()
        {
            _isOpenTelemetryCapable = null;
            _isOpenTelemetryModuleLoaded = null;
            _isOpenTelmetryEnvironmentEnabled = null;
        }

        public void AddStartOpenTelemetryInvocationCommand(OpenTelemetryInvocationContext otelContext)
        {
            if (!isOpenTelemetryCapable() || !otelContext.isValid())
            {
                return;
            }

            _pwsh.AddCommand(StartOpenTelemetryInvocationCmdlet)
                .AddParameter("InvocationId", otelContext.InvocationId)
                .AddParameter("TraceParent", otelContext.TraceParent)
                .AddParameter("TraceState", otelContext.TraceState);
        }

        public void StopOpenTelemetryInvocation(OpenTelemetryInvocationContext otelContext)
        {
            if (!isOpenTelemetryCapable())
            {
                return;
            }

            _pwsh.AddCommand(StopOpenTelemetryInvocationCmdlet)
                .AddParameter("InvocationId", otelContext.InvocationId)
                .InvokeAndClearCommands();
        }

        internal void StartFunctionsLoggingListener()
        {
            if (!isOpenTelemetryCapable())
            {
                return;
            }

            var eventHandlers = _pwsh.AddCommand(GetFunctionsLogHandlerCmdlet)
                .InvokeAndClearCommands<Action<string, string, Exception>>();

            foreach (var eventHandler in eventHandlers)
            {
                if (_logger is RpcLogger rpcLogger)
                {
                    rpcLogger.outputLogHandler.Subscribe(eventHandler);
                }
            }
        }
    }
}
