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
            // Act
            string result = OrchestrationVersionExtractor.GetVersionFromHistory(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetVersionFromHistory_ReturnsNull_WhenHistoryHasNoExecutionStartedEvent()
        {
            // Arrange
            var historyEvents = new[]
            {
                new HistoryEvent { EventType = HistoryEventType.TaskScheduled, EventId = 1 },
                new HistoryEvent { EventType = HistoryEventType.TaskCompleted, EventId = 2, TaskScheduledId = 1 }
            };

            // Act
            string result = OrchestrationVersionExtractor.GetVersionFromHistory(historyEvents);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetVersionFromHistory_ReturnsVersion_WhenExecutionStartedEventExists()
        {
            // Arrange
            const string expectedVersion = "1.0.0";
            var historyEvents = new[]
            {
                new HistoryEvent { 
                    EventType = HistoryEventType.ExecutionStarted, 
                    EventId = 1, 
                    Version = expectedVersion 
                },
                new HistoryEvent { EventType = HistoryEventType.TaskScheduled, EventId = 2 }
            };

            // Act
            string result = OrchestrationVersionExtractor.GetVersionFromHistory(historyEvents);

            // Assert
            Assert.Equal(expectedVersion, result);
        }

        [Fact]
        public void GetVersionFromHistory_ReturnsFirstExecutionStartedVersion_WhenMultipleExecutionStartedEventsExist()
        {
            // Arrange
            const string expectedVersion = "1.0.0";
            const string secondVersion = "2.0.0";
            
            var historyEvents = new[]
            {
                new HistoryEvent { 
                    EventType = HistoryEventType.ExecutionStarted, 
                    EventId = 1, 
                    Version = expectedVersion 
                },
                new HistoryEvent { EventType = HistoryEventType.TaskScheduled, EventId = 2 },
                new HistoryEvent { 
                    EventType = HistoryEventType.ExecutionStarted,
                    EventId = 3,
                    Version = secondVersion
                }
            };

            // Act
            string result = OrchestrationVersionExtractor.GetVersionFromHistory(historyEvents);

            // Assert
            Assert.Equal(expectedVersion, result);
            Assert.NotEqual(secondVersion, result);
        }
    }
}
