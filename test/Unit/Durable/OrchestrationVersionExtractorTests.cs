//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Durable
{
    using System;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable;
    using Xunit;

    public class OrchestrationVersionExtractorTests
    {
        [Fact]
        public void GetVersionFromHistory_ReturnsNull_WhenHistoryIsNull()
        {
            string result = OrchestrationVersionExtractor.GetVersionFromHistory(null);

            Assert.Null(result);
        }

        [Fact]
        public void GetVersionFromHistory_ReturnsNull_WhenHistoryHasNoExecutionStartedEvent()
        {
            var historyEvents = new[]
            {
                new HistoryEvent { EventType = HistoryEventType.OrchestratorStarted },
                new HistoryEvent { EventType = HistoryEventType.TaskScheduled }
            };

            string result = OrchestrationVersionExtractor.GetVersionFromHistory(historyEvents);

            Assert.Null(result);
        }

        [Fact]
        public void GetVersionFromHistory_ReturnsVersion_WhenExecutionStartedEventExists()
        {
            var historyEvents = new[]
            {
                new HistoryEvent { EventType = HistoryEventType.OrchestratorStarted },
                new HistoryEvent { EventType = HistoryEventType.ExecutionStarted, Version = "1.0" },
            };

            string result = OrchestrationVersionExtractor.GetVersionFromHistory(historyEvents);

            Assert.Equal("1.0", result);
        }

        [Fact]
        public void GetVersionFromHistory_ReturnsFirstExecutionStartedVersion_WhenMultipleExecutionStartedEventsExist()
        {
            var historyEvents = new[]
            {
                new HistoryEvent { EventType = HistoryEventType.ExecutionStarted, Version = "1.0" },
                new HistoryEvent { EventType = HistoryEventType.ExecutionStarted, Version = "2.0" }
            };

            string result = OrchestrationVersionExtractor.GetVersionFromHistory(historyEvents);

            Assert.Equal("1.0", result);
        }
    }
}
