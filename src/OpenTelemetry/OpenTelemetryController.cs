using System;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;

namespace Microsoft.Azure.Functions.PowerShellWorker.OpenTelemetry
{
    using PowerShell = System.Management.Automation.PowerShell;

    internal class OpenTelemetryController
    {
        private IOpenTelemetryServices _services;

        private const string OTelEnabledEnvironmentVariableName = "OTEL_FUNCTIONS_WORKER_ENABLED";

        private static bool? _isOpenTelemetryCapable;
        private static bool? _isOpenTelemetryModuleLoaded;
        private static bool? _isOpenTelmetryEnvironmentEnabled;

        public OpenTelemetryController(ILogger logger, PowerShell pwsh) 
            : this(new OpenTelemetryServices(logger, pwsh)) 
        { }

        public OpenTelemetryController(IOpenTelemetryServices services)
        {
            _services = services;
        }

        public bool IsOpenTelemetryCapable()
        {
            if (_isOpenTelemetryCapable.HasValue)
            {
                return _isOpenTelemetryCapable.Value;
            }

            _isOpenTelemetryCapable = IsOpenTelemetryEnvironmentEnabled() && IsOpenTelemetryModuleLoaded();

            return _isOpenTelemetryCapable.Value;
        }

        public static bool IsOpenTelemetryEnvironmentEnabled()
        {
            if (_isOpenTelmetryEnvironmentEnabled.HasValue)
            {
                return _isOpenTelmetryEnvironmentEnabled.Value;
            }

            string enabledEnvVarValue = Environment.GetEnvironmentVariable(OTelEnabledEnvironmentVariableName);

            if (!string.IsNullOrEmpty(enabledEnvVarValue) && bool.TryParse(enabledEnvVarValue, out bool isEnabled)) 
            {
                _isOpenTelmetryEnvironmentEnabled = isEnabled;
            }
            else
            {
                _isOpenTelmetryEnvironmentEnabled = false;
            }

            return _isOpenTelmetryEnvironmentEnabled.Value;
        }

        public bool IsOpenTelemetryModuleLoaded()
        {
            var isLoaded = _services.IsModuleLoaded();
            _isOpenTelemetryModuleLoaded = isLoaded;
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
            if (!IsOpenTelemetryCapable())
            {
                return;
            }

            _services.AddStartOpenTelemetryInvocationCommand(otelContext);
        }

        public void StopOpenTelemetryInvocation(OpenTelemetryInvocationContext otelContext, bool testing = false)
        {
            if (!IsOpenTelemetryCapable())
            {
                return;
            }

            _services.StopOpenTelemetryInvocation(otelContext, testing);
        }

        internal void StartFunctionsLoggingListener(bool testing = false)
        {
            if (!IsOpenTelemetryCapable())
            {
                return;
            }

            _services.StartFunctionsLoggingListener(testing);
        }
    }
}
