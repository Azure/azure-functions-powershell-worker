//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // Missing XML comment for publicly visible type or member 'member'

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Collections;
    using System.Management.Automation;

    /// <summary>
    /// Start the Durable Functions timer
    /// </summary>
    [Cmdlet("Start", "DurableTimer")]
    public class StartDurableTimerCommand : PSCmdlet
    {
        /// <summary>
        /// Gets and sets the duration of the Durable Timer.
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public TimeSpan Duration { get; set; }

        [Parameter]
        public SwitchParameter NoWait { get; set; }

        private readonly DurableTimer _durableTimer = new DurableTimer();

        protected override void EndProcessing()
        {
            var privateData = (Hashtable)MyInvocation.MyCommand.Module.PrivateData;
            var context = (OrchestrationContext)privateData[SetFunctionInvocationContextCommand.ContextKey];
            DateTime fireAt = context.CurrentUtcDateTime.Add(Duration);
            _durableTimer.StopAndCreateTimerOrContinue(context, fireAt, NoWait.IsPresent, WriteObject);
        }

        protected override void StopProcessing()
        {
            _durableTimer.Stop();
        }
    }
}
