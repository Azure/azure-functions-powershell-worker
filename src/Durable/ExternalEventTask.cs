//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // "Missing XML comments for publicly visible type"

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System.Linq;

    public class ExternalEventTask : DurableTask
    {
        internal string EventName { get; }

        public ExternalEventTask(string eventName)
        {
            EventName = eventName;
        }

        // There is no corresponding history event for an expected external event; we instead return a dummy HistoryEvent
        internal override HistoryEvent GetTaskScheduledHistoryEvent(OrchestrationContext context)
        {
            return new HistoryEvent();
        }

        internal override HistoryEvent GetTaskCompletedHistoryEvent(OrchestrationContext context, HistoryEvent taskScheduled)
        {
            return context.History.FirstOrDefault(
                    e => e.EventType == HistoryEventType.EventRaised &&
                         e.Name == EventName &&
                         !e.IsProcessed);
        }

        internal override OrchestrationAction CreateOrchestrationAction()
        {
            return new ExternalEventAction(EventName);
        }
    }
}
