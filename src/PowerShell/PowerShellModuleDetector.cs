using Microsoft.Azure.Functions.PowerShellWorker.Utility;
using System;
using System.Linq;
using System.Management.Automation;

namespace Microsoft.Azure.Functions.PowerShellWorker.PowerShell
{
    using LogLevel = WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

    internal class PowerShellModuleDetector
    {
        public static bool IsPowerShellModuleLoaded(System.Management.Automation.PowerShell _pwsh, ILogger _logger, string moduleName)
        {
            // Search for the module in the current session
            var matchingModules = _pwsh.AddCommand(Utils.GetModuleCmdletInfo)
                .AddParameter("FullyQualifiedName", moduleName)
                .InvokeAndClearCommands<PSModuleInfo>();

            // If we get at least one result, we know the module was imported
            var numCandidates = matchingModules.Count();
            var isModuleInCurrentSession = numCandidates > 0;

            if (isModuleInCurrentSession)
            {
                string successMessage = PowerShellWorkerStrings.FoundExternalModuleInSession;

                if (moduleName == Utils.ExternalDurableSdkName)
                {
                    successMessage = PowerShellWorkerStrings.FoundExternalDurableSdkInSession;
                }
                else if (moduleName == Utils.OpenTelemetrySdkName)
                {
                    successMessage = PowerShellWorkerStrings.FoundOpenTelemetrySdkInSession;
                }

                var candidatesInfo = matchingModules.Select(module => string.Format(successMessage, module.Name, module.Version, module.Path));
                var externalSDKModuleInfo = string.Join('\n', candidatesInfo);

                if (numCandidates > 1)
                {
                    // If there's more than 1 result, there may be runtime conflicts
                    // warn user of potential conflicts
                    _logger.Log(isUserOnlyLog: false, LogLevel.Warning, String.Format(
                        PowerShellWorkerStrings.MultipleExternalSDKsInSession,
                        numCandidates, moduleName, externalSDKModuleInfo));
                }
                else
                {
                    // a single module is in session. Report its metadata
                    _logger.Log(isUserOnlyLog: false, LogLevel.Trace, externalSDKModuleInfo);
                }
            }
            return isModuleInCurrentSession;
        }
    }
}
