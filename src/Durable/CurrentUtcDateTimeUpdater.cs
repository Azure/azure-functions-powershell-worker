//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System.Linq;

    internal static class CurrentUtcDateTimeUpdater
    {
        // Updates the CurrentUtcDateTime instance property of the OrchestrationContext to the next, unprocessed OrchestratorStarted event
        public static void UpdateCurrentUtcDateTime(OrchestrationContext context)
        {
            var newOrchestrationStart = context.History.FirstOrDefault(
                    e => e.EventType == HistoryEventType.OrchestratorStarted &&
                        !e.IsProcessed
                );
            
            if (newOrchestrationStart != null)
            {
                context.CurrentUtcDateTime = newOrchestrationStart.Timestamp.ToUniversalTime();
                newOrchestrationStart.IsProcessed = true;
            }
        }
    }
}
