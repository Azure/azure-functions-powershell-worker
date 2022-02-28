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
        private const string SetFunctionInvocationContextCommand =
            "Microsoft.Azure.Functions.PowerShellWorker\\Set-FunctionInvocationContext";
        private const string SetFunctionInvocationContextExternalCommand =
            "DurableSDK\\Set-FunctionInvocationContextExternal";

        private readonly PowerShell _pwsh;
        private bool _hasSetOrchestrationContext = false;

        public PowerShellServices(PowerShell pwsh)
        {
            _pwsh = pwsh;
        }

        public PowerShell GetPowerShell()
        {
            return this._pwsh;
        }

        public bool UsesExternalDurableSDK()
        {
            try
            {
                this._pwsh.AddCommand("Import-Module")
                    .AddParameter("Name", "DurableSDK")
                    .AddParameter("ErrorAction", ActionPreference.Stop)
                    .InvokeAndClearCommands<Action<object>>();
                 return true;
            }
            catch
            {
                return false;
            }
        }

        public void SetDurableClient(object durableClient)
        {
            var cmdlet = SetFunctionInvocationContextCommand;
            if (UsesExternalDurableSDK())
            {
                cmdlet = SetFunctionInvocationContextExternalCommand;
            }

            _pwsh.AddCommand(cmdlet)
                .AddParameter("DurableClient", durableClient)
                .InvokeAndClearCommands();

            _hasSetOrchestrationContext = true;
        }

        public OrchestrationBindingInfo SetOrchestrationContext(ParameterBinding context, out Action<object> externalInvoker)
        {
            externalInvoker = null;
            var orchBindingInfo = new OrchestrationBindingInfo(
                context.Name,
                JsonConvert.DeserializeObject<OrchestrationContext>(context.Data.String));

            if (UsesExternalDurableSDK())
            {
                Collection<Action<object>> output = _pwsh.AddCommand(SetFunctionInvocationContextExternalCommand)
                    .AddParameter("OrchestrationContext", context.Data.String)
                    .AddParameter("SetResult", (Action<object, bool>) orchBindingInfo.Context.SetExternalResult)
                    .InvokeAndClearCommands<Action<object>>();
                if (output.Count() == 1)
                {
                    externalInvoker = output[0];
                }
            }
            else
            {
                _pwsh.AddCommand(SetFunctionInvocationContextCommand)
                    .AddParameter("OrchestrationContext", orchBindingInfo.Context)
                    .InvokeAndClearCommands<Action<object>>();
            }

            _hasSetOrchestrationContext = true;
            return orchBindingInfo;
        }

        public void AddParameter(string name, object value)
        {
            _pwsh.AddParameter(name, value);
        }

        public void ClearOrchestrationContext()
        {
            var cmdlet = SetFunctionInvocationContextCommand;
            if (UsesExternalDurableSDK())
            {
                cmdlet = SetFunctionInvocationContextExternalCommand;
            }

            if (_hasSetOrchestrationContext)
            {
                _pwsh.AddCommand(cmdlet)
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
