//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // Missing XML comment for publicly visible type or member 'member'

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System.Collections;
    using System.Management.Automation;

    /// <summary>
    /// Start the Durable Functions timer
    /// </summary>
    [Cmdlet("Start", "DurableExternalEventListener")]
    public class StartDurableExternalEventListenerCommand : PSCmdlet
    {
        /// <summary>
        /// Gets and sets the duration of the Durable Timer.
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string EventName { get; set; }

        [Parameter]
        public SwitchParameter NoWait { get; set; }

        private readonly DurableTaskHandler _durableTaskHandler = new DurableTaskHandler();

        protected override void EndProcessing()
        {
            var privateData = (Hashtable)MyInvocation.MyCommand.Module.PrivateData;
            var context = (OrchestrationContext)privateData[SetFunctionInvocationContextCommand.ContextKey];
            
            var task = new ExternalEventTask(EventName);
            
            _durableTaskHandler.StopAndInitiateDurableTaskOrReplay(task, context, NoWait.IsPresent, WriteObject);
        }

        protected override void StopProcessing()
        {
            _durableTaskHandler.Stop();
        }
    }
}
