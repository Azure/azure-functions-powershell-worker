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
        private readonly PowerShell _pwsh;
        private bool _hasInitializedDurableFunctions = false;
        private bool _usesExternalDurableSDK = false;
        private readonly ErrorRecordFormatter _errorRecordFormatter = new ErrorRecordFormatter();
        private readonly ILogger _logger;

        // uses built-in SDK by default
        private string SetFunctionInvocationContextCommand = string.Format(
            PowerShellWorkerStrings.SetFunctionInvocationContextCmdLetTemplate,
            PowerShellWorkerStrings.InternalDurableSDKName);

        public PowerShellServices(PowerShell pwsh, ILogger logger)
        {
            _pwsh = pwsh;
            _logger = logger;

            // Configure FunctionInvocationContext command, based on the select DF SDK
            var prefix = PowerShellWorkerStrings.InternalDurableSDKName;
            SetFunctionInvocationContextCommand = string.Format(PowerShellWorkerStrings.SetFunctionInvocationContextCmdLetTemplate, prefix);
        }

        private bool tryImportingDurableSDK()
        {
            // Try to load/import the external Durable Functions SDK. If an error occurs, it is logged.
            var importSucceeded = false;
            try
            {
                // attempt to import SDK
                _logger.Log(isUserOnlyLog: false, LogLevel.Trace, String.Format(
                    PowerShellWorkerStrings.LoadingDurableSDK, PowerShellWorkerStrings.ExternalDurableSDKName));

                var results = _pwsh.AddCommand(Utils.ImportModuleCmdletInfo)
                    .AddParameter("FullyQualifiedName", PowerShellWorkerStrings.ExternalDurableSDKName)
                    .AddParameter("ErrorAction", ActionPreference.Stop)
                    .AddParameter("PassThru")
                    .InvokeAndClearCommands<PSModuleInfo>();

                var numResults = results.Count();
                var numExpectedResults = 1;
                if (numResults != numExpectedResults)
                {
                    _logger.Log(isUserOnlyLog: false, LogLevel.Warning, string.Format(
                               PowerShellWorkerStrings.UnexpectedResultCount, "Import Durable SDK", numExpectedResults, numResults));
                }

                // log that import succeeded
                var moduleInfo = results[0];
                _logger.Log(isUserOnlyLog: false, LogLevel.Trace, String.Format(
                    PowerShellWorkerStrings.ImportSucceeded, moduleInfo.Name, moduleInfo.Version));

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
                _logger.Log(isUserOnlyLog: true, LogLevel.Error, string.Format(
                    PowerShellWorkerStrings.ErrorImportingDurableSDK,
                    PowerShellWorkerStrings.ExternalDurableSDKName, errorMessage));

            }
            return importSucceeded;

        }

        public void tryEnablingExternalDurableSDK()
        {
            // Search for the external DF SDK in the available modules
            var matchingModules = _pwsh.AddCommand(Utils.GetModuleCmdletInfo)
                .AddParameter("ListAvailable")
                .AddParameter("FullyQualifiedName", PowerShellWorkerStrings.ExternalDurableSDKName)
                .InvokeAndClearCommands<PSModuleInfo>();

            // If we get at least one result, we attempt to load it
            var numCandidates = matchingModules.Count();
            if (numCandidates > 0)
            {
                // try to import the external DF SDK
                _usesExternalDurableSDK = tryImportingDurableSDK();
            }
            else
            {
                // Log that the module was not found in worker path
                _logger.Log(isUserOnlyLog: false, LogLevel.Trace, string.Format(
                        PowerShellWorkerStrings.DurableNotInWorkerPath, PowerShellWorkerStrings.ExternalDurableSDKName));
            }

            // assign SetFunctionInvocationContextCommand to the corresponding external SDK's CmdLet
            SetFunctionInvocationContextCommand = string.Format(
                PowerShellWorkerStrings.SetFunctionInvocationContextCmdLetTemplate,
                PowerShellWorkerStrings.ExternalDurableSDKName);
            _logger.Log(isUserOnlyLog: false, LogLevel.Trace, String.Format(
                PowerShellWorkerStrings.UtilizingExternalDurableSDK, _usesExternalDurableSDK));
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
                var numResults = output.Count();
                var numExpectedResults = 1;
                var outputContractIsMet = output.Count() == numExpectedResults;
                if (outputContractIsMet)
                {
                    externalInvoker = new ExternalInvoker(output[0]);
                }
                else
                {
                    var exceptionMessage = string.Format(PowerShellWorkerStrings.UnexpectedResultCount,
                        SetFunctionInvocationContextCommand, numExpectedResults, numResults);
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
