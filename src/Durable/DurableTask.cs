//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // "Missing XML comments for publicly visible type"

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System.Collections.Generic;
    
    public abstract class DurableTask
    {
        internal abstract HistoryEvent GetTaskScheduledHistoryEvent(OrchestrationContext context);
        
        internal abstract HistoryEvent GetTaskCompletedHistoryEvent(OrchestrationContext context, HistoryEvent taskScheduled);

        internal abstract OrchestrationAction CreateOrchestrationAction();
    }
}
