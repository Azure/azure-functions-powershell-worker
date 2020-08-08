//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // "Missing XML comments for publicly visible type"

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System.Linq;

    public class ActivityInvocationTask : DurableTask
    {
        public string Name { get; }

        internal override HistoryEvent GetTaskScheduledHistoryEvent(OrchestrationContext context)
        {
            return context.History.FirstOrDefault(
                e => e.EventType == HistoryEventType.TaskScheduled &&
                     e.Name == Name &&
                     !e.IsProcessed);
        }

        internal override HistoryEvent GetTaskCompletedHistoryEvent(OrchestrationContext context, HistoryEvent taskScheduled)
        {
            return taskScheduled == null
                ? null
                : context.History.FirstOrDefault(
                    e => e.EventType == HistoryEventType.TaskCompleted &&
                         e.TaskScheduledId == taskScheduled.EventId);
        }

        public ActivityInvocationTask(string name)
        {
            Name = name;
        }
    }
}
