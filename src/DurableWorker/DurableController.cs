//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Management.Automation;

    using Newtonsoft.Json;
    using WebJobs.Script.Grpc.Messages;

    using PowerShellWorker.Utility;
    using Microsoft.Azure.Functions.PowerShellWorker.DurableWorker;

    /// <summary>
    /// The main entry point for durable functions support.
    /// </summary>
    internal class DurableController
    {
        private readonly DurableFunctionInfo _durableFunctionInfo;
        private readonly IPowerShellServices _powerShellServices;
        private readonly IOrchestrationInvoker _orchestrationInvoker;
        private OrchestrationBindingInfo _orchestrationBindingInfo;
        private PowerShell pwsh;

        public DurableController(
            DurableFunctionInfo durableDurableFunctionInfo,
            PowerShell pwsh)
            : this(
                durableDurableFunctionInfo,
                new PowerShellServices(pwsh),
                new OrchestrationInvoker())
        {
            this.pwsh = pwsh;
        }

        internal DurableController(
            DurableFunctionInfo durableDurableFunctionInfo,
            IPowerShellServices powerShellServices,
            IOrchestrationInvoker orchestrationInvoker)
        {
            _durableFunctionInfo = durableDurableFunctionInfo;
            _powerShellServices = powerShellServices;
            _orchestrationInvoker = orchestrationInvoker;
        }

        public string GetOrchestrationParameterName()
        {
            return _orchestrationBindingInfo?.ParameterName;
        }

        public void InitializeBindings(IList<ParameterBinding> inputData)
        {
            // If the function is an orchestration client, then we set the DurableClient
            // in the module context for the 'Start-DurableOrchestration' function to use.
            if (_durableFunctionInfo.IsDurableClient)
            {
                var durableClient =
                    inputData.First(item => item.Name == _durableFunctionInfo.DurableClientBindingName)
                        .Data.ToObject();

                _powerShellServices.SetDurableClient(durableClient);

            }
            else if (_durableFunctionInfo.IsOrchestrationFunction)
            {
                try
                {
                    _orchestrationBindingInfo = CreateOrchestrationBindingInfo(inputData);
                    var context = inputData[0];
                    Collection<Action<object>> output = this.pwsh.AddCommand("Set-BindingData")
                        .AddParameter("Input", context.Data.String)
                        .AddParameter("SetResult", (Action<object, bool>)_orchestrationBindingInfo.Context.SetExternalResult)
                        .InvokeAndClearCommands<Action<object>>();
                    if (output.Count() == 1)
                    {
                        this._orchestrationInvoker.SetExternalInvoker(output[0]);
                    }

                    _powerShellServices.SetOrchestrationContext(_orchestrationBindingInfo.Context);
                }
                catch
                {
                    _orchestrationBindingInfo = CreateOrchestrationBindingInfo(inputData);
                    _powerShellServices.SetOrchestrationContext(_orchestrationBindingInfo.Context);
                }

            }
        }

        public void AfterFunctionInvocation()
        {
            _powerShellServices.ClearOrchestrationContext();
        }

        public bool TryGetInputBindingParameterValue(string bindingName, out object value)
        {
            if (_orchestrationInvoker != null
                && _orchestrationBindingInfo != null
                && string.CompareOrdinal(bindingName, _orchestrationBindingInfo.ParameterName) == 0)
            {
                value = _orchestrationBindingInfo.Context;
                return true;
            }

            value = null;
            return false;
        }

        public void AddPipelineOutputIfNecessary(Collection<object> pipelineItems, Hashtable result)
        {
 
            if (ShouldSuppressPipelineTraces())
            {
                var returnValue = FunctionReturnValueBuilder.CreateReturnValueFromFunctionOutput(pipelineItems);
                result.Add(AzFunctionInfo.DollarReturn, returnValue);
            }
        }

        public Hashtable InvokeOrchestrationFunction()
        {
            return _orchestrationInvoker.Invoke(_orchestrationBindingInfo, _powerShellServices);
        }

        public bool ShouldSuppressPipelineTraces()
        {
            return _durableFunctionInfo.Type == DurableFunctionType.ActivityFunction;
        }

        private static OrchestrationBindingInfo CreateOrchestrationBindingInfo(IList<ParameterBinding> inputData)
        {
            // Quote from https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-bindings:
            //
            // "Orchestrator functions should never use any input or output bindings other than the orchestration trigger binding.
            //  Doing so has the potential to cause problems with the Durable Task extension because those bindings may not obey the single-threading and I/O rules."
            //
            // Therefore, it's by design that input data contains only one item, which is the metadata of the orchestration context.
            var context = inputData[0];

            // TODO: make this de-serialization constructor depend on the external SDK
            return new OrchestrationBindingInfo(
                context.Name,
                JsonConvert.DeserializeObject<OrchestrationContext>(context.Data.String));
        }
    }
}
