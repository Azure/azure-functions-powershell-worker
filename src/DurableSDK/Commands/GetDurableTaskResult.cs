//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // Missing XML comment for publicly visible type or member 'member'

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable.Commands
{
    using System.Collections;
    using System.Management.Automation;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable.Tasks;

    [Cmdlet("Get", "DurableTaskResult")]
    public class GetDurableTaskResultCommand : PSCmdlet
    {
        [Parameter(Mandatory = true)]
        [ValidateNotNull]
        public DurableTask[] Task { get; set; }

        private readonly DurableTaskHandler _durableTaskHandler = new DurableTaskHandler();

        protected override void EndProcessing()
        {
            var privateData = (Hashtable)MyInvocation.MyCommand.Module.PrivateData;
            var context = (OrchestrationContext)privateData[SetFunctionInvocationContextCommand.ContextKey];

            _durableTaskHandler.GetTaskResult(Task, context, WriteObject);
        }

        protected override void StopProcessing()
        {
            _durableTaskHandler.Stop();
        }
    }
}
