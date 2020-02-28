//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Management.Automation;
    using PowerShell;

    internal class PowerShellServices : IPowerShellServices
    {
        private readonly PowerShell _pwsh;
        private bool _hasSetOrchestrationContext = false;

        public PowerShellServices(PowerShell pwsh)
        {
            _pwsh = pwsh;
        }

        public void SetOrchestrationClient(object orchestrationClient)
        {
            _pwsh.AddCommand("Microsoft.Azure.Functions.PowerShellWorker\\Set-FunctionInvocationContext")
                .AddParameter("OrchestrationClient", orchestrationClient)
                .InvokeAndClearCommands();

            _hasSetOrchestrationContext = true;
        }

        public void SetOrchestrationContext(OrchestrationContext orchestrationContext)
        {
            _pwsh.AddCommand("Microsoft.Azure.Functions.PowerShellWorker\\Set-FunctionInvocationContext")
                .AddParameter("OrchestrationContext", orchestrationContext)
                .InvokeAndClearCommands();

            _hasSetOrchestrationContext = true;
        }

        public void ClearOrchestrationContext()
        {
            if (_hasSetOrchestrationContext)
            {
                _pwsh.AddCommand("Microsoft.Azure.Functions.PowerShellWorker\\Set-FunctionInvocationContext")
                    .AddParameter("Clear", true)
                    .InvokeAndClearCommands();
            }
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
