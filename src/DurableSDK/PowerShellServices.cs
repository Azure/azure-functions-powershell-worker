//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Management.Automation;
    using Microsoft.Azure.Functions.PowerShellWorker.PowerShell;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;
    using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using LogLevel = Microsoft.Azure.WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;


    internal class PowerShellServices : IPowerShellServices
    {
        private readonly string SetFunctionInvocationContextCommand;

        private readonly PowerShell _pwsh;
        private bool hasInitializedDurableFunctions = false;
        private readonly bool hasExternalDurableSDK = false;
        private readonly ErrorRecordFormatter _errorRecordFormatter = new ErrorRecordFormatter();


        private bool tryImportingDurableSDK(PowerShell pwsh, Utility.ILogger logger)
        {
            var importSucceeded = false;
            try
            {
                pwsh.AddCommand(Utils.ImportModuleCmdletInfo)
                    .AddParameter("Name", PowerShellWorkerStrings.ExternalDurableSDKName)
                    .AddParameter("ErrorAction", ActionPreference.Stop)
                    .InvokeAndClearCommands();
                importSucceeded = true;
            }
            catch (Exception e)
            {
                var errorMessage = e.ToString();
                if (e.InnerException is IContainsErrorRecord inner)
                {
                    errorMessage = _errorRecordFormatter.Format(inner.ErrorRecord);

                }
                logger.Log(isUserOnlyLog: true, LogLevel.Error, string.Format(
                    PowerShellWorkerStrings.ErrorImportingDurableSDK,
                    PowerShellWorkerStrings.ExternalDurableSDKName,errorMessage));

            }
            return importSucceeded;

        }

        public PowerShellServices(PowerShell pwsh, Utility.ILogger logger)
        {
            // We attempt to import the external SDK upon construction of the PowerShellServices object.
            // We maintain the boolean member hasExternalDurableSDK in this object rather than
            // DurableController because the expected input and functionality of SetFunctionInvocationContextCommand
            // may differ between the internal and external implementations.

            var availableModules = pwsh.AddCommand(Utils.GetModuleCmdletInfo)
                .AddParameter("Name", PowerShellWorkerStrings.ExternalDurableSDKName)
                .InvokeAndClearCommands<PSModuleInfo>();

            var numModulesFound = availableModules.Count();
            if (numModulesFound != 1)
            {
                // default to internal SDK
                hasExternalDurableSDK = false;
                logger.Log(isUserOnlyLog: false, LogLevel.Trace, String.Format(PowerShellWorkerStrings.FailedToImportExternalDurableSDK, PowerShellWorkerStrings.ExternalDurableSDKName, numModulesFound));
            }

            else
            {

                var externalSDKInfo = availableModules[0];
                logger.Log(isUserOnlyLog: false, LogLevel.Trace, String.Format(PowerShellWorkerStrings.LoadingDurableSDK, externalSDKInfo.Name,externalSDKInfo.Version));

                hasExternalDurableSDK = tryImportingDurableSDK(pwsh, logger);
                logger.Log(isUserOnlyLog: false, LogLevel.Trace, String.Format(PowerShellWorkerStrings.UtilizingExternalDurableSDK, hasExternalDurableSDK));
            }

            var templatedSetFunctionInvocationContextCommand = "{0}\\Set-FunctionInvocationContext";
            var prefix = hasExternalDurableSDK ? PowerShellWorkerStrings.ExternalDurableSDKName : PowerShellWorkerStrings.InternalDurableSDKName;
            SetFunctionInvocationContextCommand = string.Format(templatedSetFunctionInvocationContextCommand, prefix);
            
                _pwsh = pwsh;
        }

        public bool HasExternalDurableSDK()
        {
            return hasExternalDurableSDK;
        }

        public PowerShell GetPowerShell()
        {
            return this._pwsh;
        }

        public void SetDurableClient(object durableClient)
        {
            _pwsh.AddCommand(SetFunctionInvocationContextCommand)
                .AddParameter("DurableClient", durableClient)
                .InvokeAndClearCommands();
            hasInitializedDurableFunctions = true;
        }

        public OrchestrationBindingInfo SetOrchestrationContext(
            ParameterBinding context,
            out IExternalOrchestrationInvoker externalInvoker)
        {
            externalInvoker = null;
            OrchestrationBindingInfo orchestrationBindingInfo = new OrchestrationBindingInfo(
                context.Name,
                JsonConvert.DeserializeObject<OrchestrationContext>(context.Data.String));

            if (hasExternalDurableSDK)
            {
                Collection<Func<PowerShell, object>> output = _pwsh.AddCommand(SetFunctionInvocationContextCommand)
                    // The external SetFunctionInvocationContextCommand expects a .json string to deserialize
                    // and writes an invoker function to the output pipeline.
                    .AddParameter("OrchestrationContext", context.Data.String)
                    .InvokeAndClearCommands<Func<PowerShell, object>>();

                // If more than 1 element is present in the output pipeline, we cannot trust that we have
                // obtained the external orchestrator invoker; i.e the output contract is not met.
                var outputContractIsMet = output.Count() == 1;
                if (outputContractIsMet)
                {
                    externalInvoker = new ExternalInvoker(output[0]);
                }
                else
                {
                    var exceptionMessage = string.Format(PowerShellWorkerStrings.UnexpectedOutputInExternalDurableCommand, SetFunctionInvocationContextCommand);
                    throw new InvalidOperationException(exceptionMessage);
                }
            }
            else
            {
                _pwsh.AddCommand(SetFunctionInvocationContextCommand)
                    .AddParameter("OrchestrationContext", orchestrationBindingInfo.Context)
                    .InvokeAndClearCommands();
            }
            hasInitializedDurableFunctions = true;
            return orchestrationBindingInfo;
        }
        

        public void AddParameter(string name, object value)
        {
            _pwsh.AddParameter(name, value);
        }

        public void ClearOrchestrationContext()
        {
            if (hasInitializedDurableFunctions)
            {
                _pwsh.AddCommand(SetFunctionInvocationContextCommand)
                    .AddParameter("Clear", true)
                    .InvokeAndClearCommands();
            }
        }

        public void TracePipelineObject()
        {
            _pwsh.AddCommand(PowerShellWorkerStrings.TracePipelineObjectCommand);
        }

        public IAsyncResult BeginInvoke(PSDataCollection<object> output)
        {
            return _pwsh.BeginInvoke<object, object>(input: null, output);
        }

        public void EndInvoke(IAsyncResult asyncResult)
        {
            _pwsh.EndInvoke(asyncResult);
        }

        public void StopInvoke()
        {
            _pwsh.Stop();
        }

        public void ClearStreamsAndCommands()
        {
            _pwsh.Streams.ClearStreams();
            _pwsh.Commands.Clear();
        }
    }
}
