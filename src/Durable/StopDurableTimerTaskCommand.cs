//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // Missing XML comment for publicly visible type or member 'member'

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System.Management.Automation;

    /// <summary>
    /// Start the Durable Functions timer
    /// </summary>
    [Cmdlet("Stop", "DurableTimerTask")]
    public class StopDurableTimerCommand : PSCmdlet
    {
        /// <summary>
        /// Gets and sets the duration of the Durable Timer.
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public DurableTimerTask TimerTask { get; set; }

        protected override void EndProcessing()
        {
            TimerTask.Cancel();
        }
    }
}
