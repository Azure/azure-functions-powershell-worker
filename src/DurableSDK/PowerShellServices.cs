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

    internal class PowerShellServices : IPowerShellServices
    {
        private readonly PowerShell _pwsh;
        private bool _hasInitializedDurableFunctions = false;
        private bool _usesExternalDurableSDK = false;
        private readonly ErrorRecordFormatter _errorRecordFormatter = new ErrorRecordFormatter();
        private readonly ILogger _logger;

        private const string _setFunctionInvocationContextCommandTemplate = "{0}\\Set-FunctionInvocationContext";

        // uses built-in SDK by default
        private string SetFunctionInvocationContextCommand = string.Format(
            _setFunctionInvocationContextCommandTemplate,
            Utils.InternalDurableSdkName);

        public PowerShellServices(PowerShell pwsh, ILogger logger)
        {
            _pwsh = pwsh;
            _logger = logger;

            // Configure FunctionInvocationContext command, based on the select DF SDK
            var prefix = Utils.InternalDurableSdkName;
            SetFunctionInvocationContextCommand = string.Format(_setFunctionInvocationContextCommandTemplate, prefix);
        }

        public bool isExternalDurableSdkLoaded()
        {
            return PowerShellModuleDetector.IsPowerShellModuleLoaded(_pwsh, _logger, Utils.ExternalDurableSdkName);
        }

        public void EnableExternalDurableSDK()
        {
            _usesExternalDurableSDK = true;

            // assign SetFunctionInvocationContextCommand to the corresponding external SDK's CmdLet
            SetFunctionInvocationContextCommand = string.Format(
               _setFunctionInvocationContextCommandTemplate,
                Utils.ExternalDurableSdkName);
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
            _pwsh.AddCommand(Utils.TracePipelineObjectCmdlet);
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
