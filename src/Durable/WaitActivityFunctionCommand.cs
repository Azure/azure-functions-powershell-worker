//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // Missing XML comment for publicly visible type or member 'member'

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System.Collections;
    using System.Management.Automation;

    [Cmdlet("Wait", "ActivityFunction")]
    public class WaitActivityFunctionCommand : PSCmdlet
    {
        [Parameter(Mandatory = true)]
        [ValidateNotNull]
        public ActivityInvocationTask[] Task { get; set; }

        private readonly DurableTaskHandler _durableTaskHandler = new DurableTaskHandler();

        protected override void EndProcessing()
        {
            var privateData = (Hashtable)MyInvocation.MyCommand.Module.PrivateData;
            var context = (OrchestrationContext)privateData[SetFunctionInvocationContextCommand.ContextKey];
            _durableTaskHandler.WaitAll(Task, context, WriteObject);
        }

        protected override void StopProcessing()
        {
            _durableTaskHandler.Stop();
        }
    }
}
