using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Functions.PowerShellWorker.OpenTelemetry
{
    internal interface IPowerShellServicesForOpenTelemetry
    {
        bool? IsModuleLoaded();
        void AddStartOpenTelemetryInvocationCommand(OpenTelemetryInvocationContext otelContext);
        void StopOpenTelemetryInvocation(OpenTelemetryInvocationContext otelContext, bool invokeCommands);
        void StartFunctionsLoggingListener(bool invokeCommands);
    }
}
