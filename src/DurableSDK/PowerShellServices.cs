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
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;
    using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
    using Newtonsoft.Json;

    internal class PowerShellServices : IPowerShellServices
    {
        private readonly string SetFunctionInvocationContextCommand;
        private const string ExternalDurableSDKName = "DurableSDK";
        private const string InternalDurableSDKName = "Microsoft.Azure.Functions.PowerShellWorker";

        private readonly PowerShell _pwsh;
        private bool _hasInitializedDurableFunction = false;
        private readonly bool _useExternalDurableSDK = false;

        public PowerShellServices(PowerShell pwsh)
        {
            /* This logic will be commented out until the external SDK is published on the PS Gallery

            // We attempt to import the external SDK upon construction of the PowerShellServices object.
            // We maintain the boolean member _useExternalDurableSDK in this object rather than
            // DurableController because the expected input and functionality of SetFunctionInvocationContextCommand
            // may differ between the internal and external implementations.

            try
            {
                pwsh.AddCommand(Utils.ImportModuleCmdletInfo)
                    .AddParameter("Name", ExternalDurableSDKName)
                    .AddParameter("ErrorAction", ActionPreference.Stop)
                    .InvokeAndClearCommands();
                _useExternalDurableSDK = true;
            }
            catch (Exception e)
            {
                // Check to see if ExternalDurableSDK is among the modules imported or
                // available to be imported: if it is, then something went wrong with
                // the Import-Module statement and we should throw an Exception.
                // Otherwise, we use the InternalDurableSDK
                var availableModules = pwsh.AddCommand(Utils.GetModuleCmdletInfo)
                    .AddParameter("Name", ExternalDurableSDKName)
                    .InvokeAndClearCommands<PSModuleInfo>();
                if (availableModules.Count() > 0)
                {
                    // TODO: evaluate if there is a better error message or exception type to be throwing.
                    // Ideally, this should never happen.
                    throw new InvalidOperationException("The external Durable SDK was detected, but unable to be imported.", e);
                }
                _useExternalDurableSDK = false;
            }*/
            _useExternalDurableSDK = false;

            if (_useExternalDurableSDK)
            {
                SetFunctionInvocationContextCommand = $"{ExternalDurableSDKName}\\Set-FunctionInvocationContext";
            }
            else
            {
                SetFunctionInvocationContextCommand = $"{InternalDurableSDKName}\\Set-FunctionInvocationContext";
            }
            _pwsh = pwsh;
        }

        public bool UseExternalDurableSDK()
        {
            return _useExternalDurableSDK;
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
            _hasInitializedDurableFunction = true;
        }

        public OrchestrationBindingInfo SetOrchestrationContext(
            ParameterBinding context,
            out IExternalInvoker externalInvoker)
        {
            externalInvoker = null;
            OrchestrationBindingInfo orchestrationBindingInfo = new OrchestrationBindingInfo(
                context.Name,
                JsonConvert.DeserializeObject<OrchestrationContext>(context.Data.String));

            if (_useExternalDurableSDK)
            {
                Collection<Func<PowerShell, object>> output = _pwsh.AddCommand(SetFunctionInvocationContextCommand)
                    // The external SetFunctionInvocationContextCommand expects a .json string to deserialize
                    // and writes an invoker function to the output pipeline.
                    .AddParameter("OrchestrationContext", context.Data.String)
                    .InvokeAndClearCommands<Func<PowerShell, object>>();
                if (output.Count() == 1)
                {
                    externalInvoker = new ExternalInvoker(output[0]);
                }
                else
                {
                    throw new InvalidOperationException($"Only a single output was expected for an invocation of {SetFunctionInvocationContextCommand}");
                }
            }
            else
            {
                _pwsh.AddCommand(SetFunctionInvocationContextCommand)
                    .AddParameter("OrchestrationContext", orchestrationBindingInfo.Context)
                    .InvokeAndClearCommands();
            }
            _hasInitializedDurableFunction = true;
            return orchestrationBindingInfo;
        }
        

        public void AddParameter(string name, object value)
        {
            _pwsh.AddParameter(name, value);
        }

        public void ClearOrchestrationContext()
        {
            if (_hasInitializedDurableFunction)
            {
                _pwsh.AddCommand(SetFunctionInvocationContextCommand)
                    .AddParameter("Clear", true)
                    .InvokeAndClearCommands();
            }
        }

        public void TracePipelineObject()
        {
            _pwsh.AddCommand("Microsoft.Azure.Functions.PowerShellWorker\\Trace-PipelineObject");
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
