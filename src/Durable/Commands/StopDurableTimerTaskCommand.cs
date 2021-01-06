//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // Missing XML comment for publicly visible type or member 'member'

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable.Commands
{
    using System.Management.Automation;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable.Tasks;

    /// <summary>
    /// Stop the Durable timeout task
    /// </summary>
    [Cmdlet("Stop", "DurableTimerTask")]
    public class StopDurableTimerCommand : PSCmdlet
    {
        /// <summary>
        /// Gets and sets the task to be stopped.
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public DurableTimerTask Task { get; set; }

        protected override void EndProcessing()
        {
            Task.Cancel();
        }
    }
}
