//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;

    /// <summary>
    /// Helper class to extract version information from orchestration context.
    /// </summary>
    internal static class OrchestrationVersionExtractor
    {
        /// <summary>
        /// Gets the orchestration version from a collection of history events.
        /// </summary>
        /// <param name="historyEvents">The history events to search.</param>
        /// <returns>The version, or null if not found.</returns>
        public static string GetVersionFromHistory(HistoryEvent[] historyEvents)
        {
            if (historyEvents == null)
            {
                return null;
            }
            
            var executionStartedEvent = Array.Find(historyEvents, e => e.EventType == HistoryEventType.ExecutionStarted);
            return executionStartedEvent?.Version;
        }
    }
}
