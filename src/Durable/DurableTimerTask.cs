//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // "Missing XML comments for publicly visible type"

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;

    // Returned by the Start-DurableTimer cmdlet if the NoWait flag is present, representing a timeout task
    // All DurableTimerTasks must be complete or canceled for the orchestration to complete
    public class DurableTimerTask
    {
        public DateTime FireAt { get; set; }

        // Only incomplete, uncanceled DurableTimerTasks should be created
        internal DurableTimerTask(
            DateTime fireAt)
        {
            FireAt = fireAt;
        }
    }
}
