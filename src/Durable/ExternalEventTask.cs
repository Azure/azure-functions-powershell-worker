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
        internal string ExternalEventName { get; }

        public ExternalEventTask(string externalEventName)
        {
            ExternalEventName = externalEventName;
        }

        // There is no corresponding history event for an expected external event
        internal override HistoryEvent GetScheduledHistoryEvent(OrchestrationContext context)
        {
            return null;
        }

        internal override HistoryEvent GetCompletedHistoryEvent(OrchestrationContext context, HistoryEvent taskScheduled)
        {
            return context.History.FirstOrDefault(
                    e => e.EventType == HistoryEventType.EventRaised &&
                         e.Name == ExternalEventName &&
                         !e.IsProcessed);
        }

        internal override OrchestrationAction CreateOrchestrationAction()
        {
            return new ExternalEventAction(ExternalEventName);
        }
    }
}
