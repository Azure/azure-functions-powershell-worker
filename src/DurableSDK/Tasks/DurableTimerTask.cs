//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // "Missing XML comments for publicly visible type"

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable.Tasks
{
    using System;
    using System.Linq;

    using Microsoft.Azure.Functions.PowerShellWorker.Durable.Actions;

    // Returned by the Start-DurableTimer cmdlet if the NoWait flag is present, representing a timeout task
    // All DurableTimerTasks must be complete or canceled for the orchestration to complete
    public class DurableTimerTask : DurableTask
    {
        internal DateTime FireAt { get; }

        private CreateDurableTimerAction Action { get; }

        // Only incomplete, uncanceled DurableTimerTasks should be created
        internal DurableTimerTask(
            DateTime fireAt)
        {
            FireAt = fireAt;
            Action = new CreateDurableTimerAction(FireAt);
        }

        internal override HistoryEvent GetScheduledHistoryEvent(OrchestrationContext context)
        {
            return context.History.FirstOrDefault(
                e => e.EventType == HistoryEventType.TimerCreated &&
                     e.FireAt.Equals(FireAt) &&
                     !e.IsProcessed);
        }

        internal override HistoryEvent GetCompletedHistoryEvent(OrchestrationContext context, HistoryEvent scheduledHistoryEvent)
        {
            return scheduledHistoryEvent == null
                ? null
                : context.History.FirstOrDefault(
                    e => e.EventType == HistoryEventType.TimerFired &&
                         e.TimerId == scheduledHistoryEvent.EventId);
        }

        internal override OrchestrationAction CreateOrchestrationAction()
        {
            return Action;
        }

        // Indicates that the task has been canceled; without this, the orchestration will not terminate until the timer has expired
        internal void Cancel()
        {
            Action.IsCanceled = true;
        }
    }
}
