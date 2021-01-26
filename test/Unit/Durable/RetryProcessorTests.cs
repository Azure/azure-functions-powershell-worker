//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Durable
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable;
    using Xunit;

    public class RetryProcessorTests
    {
        [Fact]
        public void ContinuesAfterFirstFailure()
        {
            var history = new[]
            {
                new HistoryEvent { EventType = HistoryEventType.TaskScheduled, EventId = 1 },
                new HistoryEvent { EventType = HistoryEventType.TaskFailed,    EventId = -1, TaskScheduledId = 1, Reason = "Failure 1" }
            };

            AssertRetryProcessorReportsContinue(history, firstEventIndex: 0, maxNumberOfAttempts: 2);
            AssertNoEventsProcessed(history);
        }

        [Fact]
        public void ContinuesAfterSecondFailure()
        {
            var history = new[]
            {
                new HistoryEvent { EventType = HistoryEventType.TaskScheduled, EventId = 1 },
                new HistoryEvent { EventType = HistoryEventType.TaskFailed,    EventId = -1, TaskScheduledId = 1, Reason = "Failure 1" },
                new HistoryEvent { EventType = HistoryEventType.TimerCreated,  EventId = 2 },
                new HistoryEvent { EventType = HistoryEventType.TimerFired,    EventId = -1, TimerId = 2 },

                new HistoryEvent { EventType = HistoryEventType.TaskScheduled, EventId = 3 },
                new HistoryEvent { EventType = HistoryEventType.TaskFailed,    EventId = -1, TaskScheduledId = 3, Reason = "Failure 2" },
            };

            AssertRetryProcessorReportsContinue(history, firstEventIndex: 0, maxNumberOfAttempts: 2);
            AssertEventsProcessed(history, 0, 1, 2, 3); // Don't expect the last Scheduled/Failed pair to be processed
        }

        [Fact]
        public void FailsOnMaxNumberOfAttempts()
        {
            var history = new[]
            {
                new HistoryEvent { EventType = HistoryEventType.TaskScheduled, EventId = 1 },
                new HistoryEvent { EventType = HistoryEventType.TaskFailed,    EventId = -1, TaskScheduledId = 1, Reason = "Failure 1" },
                new HistoryEvent { EventType = HistoryEventType.TimerCreated,  EventId = 2 },
                new HistoryEvent { EventType = HistoryEventType.TimerFired,    EventId = -1, TimerId = 2 },

                new HistoryEvent { EventType = HistoryEventType.TaskScheduled, EventId = 3 },
                new HistoryEvent { EventType = HistoryEventType.TaskFailed,    EventId = -1, TaskScheduledId = 3, Reason = "Failure 2" },
                new HistoryEvent { EventType = HistoryEventType.TimerCreated,  EventId = 4 },
                new HistoryEvent { EventType = HistoryEventType.TimerFired,    EventId = -1, TimerId = 4 },
            };

            AssertRetryProcessorReportsFailure(history, firstEventIndex: 0, maxNumberOfAttempts: 2, "Failure 2");
            AssertAllEventsProcessed(history);
        }

        [Fact]
        public void SucceedsOnRetry()
        {
            var history = new[]
            {
                new HistoryEvent { EventType = HistoryEventType.TaskScheduled, EventId = 1 },
                new HistoryEvent { EventType = HistoryEventType.TaskFailed,    EventId = -1, TaskScheduledId = 1, Reason = "Failure 1" },
                new HistoryEvent { EventType = HistoryEventType.TimerCreated,  EventId = 2 },
                new HistoryEvent { EventType = HistoryEventType.TimerFired,    EventId = -1, TimerId = 2 },

                new HistoryEvent { EventType = HistoryEventType.TaskScheduled, EventId = 3 },
                new HistoryEvent { EventType = HistoryEventType.TaskCompleted, EventId = -1, TaskScheduledId = 3, Result = "Success" },
            };

            AssertRetryProcessorReportsSuccess(history, firstEventIndex: 0, maxNumberOfAttempts: 2, "Success");
            AssertAllEventsProcessed(history);
        }

        [Fact]
        public void IgnoresPreviousHistory()
        {
            var history = new[]
            {
                // From a previous activity invocation
                new HistoryEvent { EventType = HistoryEventType.TaskScheduled, EventId = 1 },
                new HistoryEvent { EventType = HistoryEventType.TaskFailed,    EventId = -1, TaskScheduledId = 1, Reason = "Failure 1" },
                new HistoryEvent { EventType = HistoryEventType.TimerCreated,  EventId = 2 },
                new HistoryEvent { EventType = HistoryEventType.TimerFired,    EventId = -1, TimerId = 2 },

                new HistoryEvent { EventType = HistoryEventType.TaskScheduled, EventId = 3 },
                new HistoryEvent { EventType = HistoryEventType.TaskCompleted, EventId = -1, TaskScheduledId = 3, Result = "Success 1" },

                // The current invocation starts here:
                new HistoryEvent { EventType = HistoryEventType.TaskScheduled, EventId = 4 },
                new HistoryEvent { EventType = HistoryEventType.TaskFailed,    EventId = -1, TaskScheduledId = 4, Reason = "Failure 2" },
                new HistoryEvent { EventType = HistoryEventType.TimerCreated,  EventId = 5 },
                new HistoryEvent { EventType = HistoryEventType.TimerFired,    EventId = -1, TimerId = 5 },

                new HistoryEvent { EventType = HistoryEventType.TaskScheduled, EventId = 6 },
                new HistoryEvent { EventType = HistoryEventType.TaskCompleted, EventId = -1, TaskScheduledId = 6, Result = "Success 2" },
            };

            AssertRetryProcessorReportsSuccess(history, firstEventIndex: 6, maxNumberOfAttempts: 2, "Success 2");
            AssertEventsProcessed(history, 6, 7, 8, 9, 10, 11);
        }

        // This history emulates the situation when multiple activity invocations are scheduled at the same time
        // ("fan-out" scenario):
        //   - Activity A failed on the first attempt and succeeded on the second attempt.
        //   - Activity B failed after two attempts.
        //   - Activity C failed on the first attempt and has not been retried yet.
        private static HistoryEvent[] CreateInterleavingHistory()
        {
            return new[]
            {
                new HistoryEvent { EventType = HistoryEventType.TaskScheduled, EventId = 1 },                                        //  0: A
                new HistoryEvent { EventType = HistoryEventType.TaskScheduled, EventId = 2 },                                        //  1: B
                new HistoryEvent { EventType = HistoryEventType.TaskScheduled, EventId = 3 },                                        //  2: C
                new HistoryEvent { EventType = HistoryEventType.TaskFailed,    EventId = -1, TaskScheduledId = 1, Reason = "A1" },   //  3: A
                new HistoryEvent { EventType = HistoryEventType.TimerCreated,  EventId = 4 },                                        //  4: A
                new HistoryEvent { EventType = HistoryEventType.TaskFailed,    EventId = -1, TaskScheduledId = 2, Reason = "B1" },   //  5: B
                new HistoryEvent { EventType = HistoryEventType.TimerCreated,  EventId = 5 },                                        //  6: B
                new HistoryEvent { EventType = HistoryEventType.TimerFired,    EventId = -1, TimerId = 4 },                          //  7: A
                new HistoryEvent { EventType = HistoryEventType.TaskScheduled, EventId = 6 },                                        //  8: A
                new HistoryEvent { EventType = HistoryEventType.TimerFired,    EventId = -1, TimerId = 5 },                          //  9: B
                new HistoryEvent { EventType = HistoryEventType.TaskScheduled, EventId = 7 },                                        // 10: B
                new HistoryEvent { EventType = HistoryEventType.TaskCompleted, EventId = -1, TaskScheduledId = 6, Result = "OK" },   // 11: A
                new HistoryEvent { EventType = HistoryEventType.TaskFailed,    EventId = -1, TaskScheduledId = 7, Reason = "B2" },   // 12: B
                new HistoryEvent { EventType = HistoryEventType.TimerCreated,  EventId = 8 },                                        // 13: B
                new HistoryEvent { EventType = HistoryEventType.TimerFired,    EventId = -1, TimerId = 8 },                          // 14: B
                new HistoryEvent { EventType = HistoryEventType.TaskFailed,    EventId = -1, TaskScheduledId = 3, Reason = "C1" },   // 15: C
            };
        }

        [Fact]
        public void InterleavingRetries_ReportsSuccess()
        {
            var history = CreateInterleavingHistory();

            // Activity A
            AssertRetryProcessorReportsSuccess(history, firstEventIndex: 0, maxNumberOfAttempts: 2, "OK");
            AssertEventsProcessed(history, 0, 3, 4, 7, 8, 11);
        }

        [Fact]
        public void InterleavingRetries_ReportsFailure()
        {
            var history = CreateInterleavingHistory();

            // Activity B
            AssertRetryProcessorReportsFailure(history, firstEventIndex: 1, maxNumberOfAttempts: 2, "B2");
            AssertEventsProcessed(history, 1, 5, 6, 9, 10, 12, 13, 14);
        }

        [Fact]
        public void InterleavingRetries_ReportsContinue()
        {
            var history = CreateInterleavingHistory();

            // Activity C
            AssertRetryProcessorReportsContinue(history, firstEventIndex: 2, maxNumberOfAttempts: 2);
            AssertNoEventsProcessed(history);
        }

        private static void AssertRetryProcessorReportsContinue(HistoryEvent[] history, int firstEventIndex, int maxNumberOfAttempts)
        {
            var shouldRetry = RetryProcessor.Process(
                history,
                history[firstEventIndex],
                maxNumberOfAttempts,
                onSuccess: result => { Assert.True(false, $"Unexpected output: {result}"); },
                onFinalFailure: reason => { Assert.True(false, $"Unexpected failure: {reason}"); });

            Assert.True(shouldRetry);
        }

        private static void AssertRetryProcessorReportsFailure(HistoryEvent[] history, int firstEventIndex, int maxNumberOfAttempts, string expectedFailureReason)
        {
            string actualFailureReason = null;

            var shouldRetry = RetryProcessor.Process(
                history,
                history[firstEventIndex],
                maxNumberOfAttempts,
                onSuccess: result => { Assert.True(false, $"Unexpected output: {result}"); },
                onFinalFailure: reason =>
                {
                    Assert.Null(actualFailureReason);
                    actualFailureReason = reason;
                });

            Assert.False(shouldRetry);
            Assert.Equal(expectedFailureReason, actualFailureReason);
        }

        private static void AssertRetryProcessorReportsSuccess(HistoryEvent[] history, int firstEventIndex, int maxNumberOfAttempts, string expectedOutput)
        {
            string actualOutput = null;

            var shouldRetry = RetryProcessor.Process(
                history,
                history[firstEventIndex],
                maxNumberOfAttempts,
                onSuccess: result =>
                    {
                        Assert.Null(actualOutput);
                        actualOutput = result;
                    },
                onFinalFailure: reason => { Assert.True(false, $"Unexpected failure: {reason}"); });

            Assert.False(shouldRetry);
            Assert.Equal(expectedOutput, actualOutput);
        }

        private static void AssertEventsProcessed(HistoryEvent[] history, params int[] expectedProcessedIndexes)
        {
            for (var i = 0; i < history.Length; ++i)
            {
                var expectedProcessed = expectedProcessedIndexes.Contains(i);
                Assert.Equal(expectedProcessed, history[i].IsProcessed);
            }
        }

        private static void AssertAllEventsProcessed(HistoryEvent[] history)
        {
            Assert.True(history.All(e => e.IsProcessed));
        }

        private static void AssertNoEventsProcessed(HistoryEvent[] history)
        {
            AssertEventsProcessed(history); // Note: passing nothing to expectedProcessedIndexes
        }
    }
}
