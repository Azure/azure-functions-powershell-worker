//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // "Missing XML comments for publicly visible type"

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    public abstract class DurableTask
    {
        // GetScheduledHistoryEvent should never return null unless the task has not been initiated
        internal abstract HistoryEvent GetScheduledHistoryEvent(OrchestrationContext context);
        
        internal abstract HistoryEvent GetCompletedHistoryEvent(OrchestrationContext context, HistoryEvent scheduledHistoryEvent);

        internal abstract OrchestrationAction CreateOrchestrationAction();
    }
}
