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
    using Newtonsoft.Json;
    using LogLevel = WebJobs.Script.Grpc.Messages.RpcLog.Types.Level;

    internal class PowerShellServices : IPowerShellServices
    {
        private readonly string SetFunctionInvocationContextCommand;

        private readonly PowerShell _pwsh;
        private bool _hasInitializedDurableFunctions = false;
        private readonly bool _usesExternalDurableSDK = false;
        private readonly ErrorRecordFormatter _errorRecordFormatter = new ErrorRecordFormatter();


        private bool tryImportingDurableSDK(PowerShell pwsh, ILogger logger)
        {
            // Try to load/import the external Durable Functions SDK. If an error occurs, it is logged.
            var importSucceeded = false;
            try
            {
                // attempt to load
                pwsh.AddCommand(Utils.ImportModuleCmdletInfo)
                    .AddParameter("Name", PowerShellWorkerStrings.ExternalDurableSDKName)
                    .AddParameter("ErrorAction", ActionPreference.Stop)
                    .InvokeAndClearCommands();
                importSucceeded = true;
            }
            catch (Exception e)
            {
                // If an error ocurred, we try to log the exception.
                var errorMessage = e.ToString();

                // If a PowerShell error record is available through Get-Error, we log that instead.
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

        public PowerShellServices(PowerShell pwsh, ILogger logger)
        {
            // We attempt to import the external DF SDK upon construction of the PowerShellServices object.
            // We also maintain the boolean member hasExternalDurableSDK in this object rather than
            // DurableController because the expected input and functionality of SetFunctionInvocationContextCommand
            // may differ between the internal and external implementations.

            // First, we search for the external DF SDK in the available modules
            var matchingModules = pwsh.AddCommand(Utils.GetModuleCmdletInfo)
                .AddParameter("Name", PowerShellWorkerStrings.ExternalDurableSDKName)
                .InvokeAndClearCommands<PSModuleInfo>();

            // To load the external SDK, we expect there to be a single matching module.
            var numCandidates = matchingModules.Count();
            if (numCandidates != 1)
            {
                // If we do not find exactly one matching module, we default to the built-in SDK.
                // Although it is unlikely (or impossible), if there were more than 1 result, we do not want to determine the "right" module.
                _usesExternalDurableSDK = false;
                logger.Log(isUserOnlyLog: false, LogLevel.Trace, String.Format(
                    PowerShellWorkerStrings.FailedToImportExternalDurableSDK,
                    PowerShellWorkerStrings.ExternalDurableSDKName, numCandidates));
            }

            else
            {
                // We found a singular instance of the external DF SDK. We log its name and version, and try to load it.
                var externalSDKInfo = matchingModules[0];
                logger.Log(isUserOnlyLog: false, LogLevel.Trace, String.Format(PowerShellWorkerStrings.LoadingDurableSDK, externalSDKInfo.Name, externalSDKInfo.Version));

                _usesExternalDurableSDK = tryImportingDurableSDK(pwsh, logger);
                logger.Log(isUserOnlyLog: false, LogLevel.Trace, String.Format(PowerShellWorkerStrings.UtilizingExternalDurableSDK, _usesExternalDurableSDK));
            }

            var templatedSetFunctionInvocationContextCommand = "{0}\\Set-FunctionInvocationContext";
            var prefix = _usesExternalDurableSDK ? PowerShellWorkerStrings.ExternalDurableSDKName : PowerShellWorkerStrings.InternalDurableSDKName;
            SetFunctionInvocationContextCommand = string.Format(templatedSetFunctionInvocationContextCommand, prefix);
            
                _pwsh = pwsh;
        }

        public bool HasExternalDurableSDK()
        {
            return _usesExternalDurableSDK;
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
            _hasInitializedDurableFunctions = true;
        }

        public OrchestrationBindingInfo SetOrchestrationContext(
            ParameterBinding context,
            out IExternalOrchestrationInvoker externalInvoker)
        {
            externalInvoker = null;
            OrchestrationBindingInfo orchestrationBindingInfo = new OrchestrationBindingInfo(
                context.Name,
                JsonConvert.DeserializeObject<OrchestrationContext>(context.Data.String));

            if (_usesExternalDurableSDK)
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
            _hasInitializedDurableFunctions = true;
            return orchestrationBindingInfo;
        }
        

        public void AddParameter(string name, object value)
        {
            _pwsh.AddParameter(name, value);
        }

        public void ClearOrchestrationContext()
        {
            if (_hasInitializedDurableFunctions)
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
