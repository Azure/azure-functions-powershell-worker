//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // "Missing XML comments for publicly visible type"

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Linq;

    // Returned by the Start-DurableTimer cmdlet if the NoWait flag is present, representing a timeout task
    // All DurableTimerTasks must be complete or canceled for the orchestration to complete
    public class DurableTimerTask : DurableTask
    {
        public DateTime FireAt { get; set; }

        // Only incomplete, uncanceled DurableTimerTasks should be created
        internal DurableTimerTask(
            DateTime fireAt)
        {
            FireAt = fireAt;
        }

        internal override HistoryEvent GetTaskScheduledHistoryEvent(OrchestrationContext context)
        {
            return context.History.FirstOrDefault(
                e => e.EventType == HistoryEventType.TimerCreated &&
                     e.FireAt.Equals(FireAt) &&
                     !e.IsProcessed);
        }

        internal override HistoryEvent GetTaskCompletedHistoryEvent(OrchestrationContext context, HistoryEvent taskScheduled)
        {
            return taskScheduled == null
                ? null
                : context.History.FirstOrDefault(
                    e => e.EventType == HistoryEventType.TimerFired &&
                         e.TimerId == taskScheduled.EventId);
        }

        internal override OrchestrationAction CreateOrchestrationAction()
        {
            return new CreateDurableTimerAction(FireAt);
        }
    }
}
