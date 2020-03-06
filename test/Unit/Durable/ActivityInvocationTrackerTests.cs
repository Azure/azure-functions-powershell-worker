//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Durable
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable;
    using Xunit;

    public class ActivityInvocationTrackerTests
    {
        private const string FunctionName = "function name";
        private const string FunctionInput = "function input";
        private const string InvocationResult = "Invocation result";
        private const string InvocationResultJson = "\"Invocation result\"";

        [Theory]
        [InlineData(true, false, true)]
        public void ReplayActivityOrStop_ReplaysActivity_WhenHistoryContainsUnprocessedCompletionEvent(
            bool scheduled, bool processed, bool completed)
        {
            var history = CreateHistory(scheduled: scheduled, processed: processed, completed: completed, output: InvocationResultJson);
            var orchestrationContext = new OrchestrationContext { History = history };

            var activityInvocationTracker = new ActivityInvocationTracker();
            activityInvocationTracker.ReplayActivityOrStop(FunctionName, FunctionInput, orchestrationContext, output => { Assert.Equal(InvocationResult, output); });

            VerifyCallActivityActionAdded(orchestrationContext);
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, false)]
        [InlineData(false, true, true)]
        [InlineData(true, false, false)]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        public void ReplayActivityOrStop_Stops_WhenHistoryDoesNotContainUnprocessedCompletionEvent(
            bool scheduled, bool processed, bool completed)
        {
            var history = CreateHistory(scheduled: scheduled, processed: processed, completed: completed, output: InvocationResultJson);
            var orchestrationContext = new OrchestrationContext { History = history };

            var activityInvocationTracker = new ActivityInvocationTracker();

            // In the actual usage, Stop is supposed to be invoked from another thread.
            // However, in order to simplify this test and avoid spawning threads,
            // waiting for them, and handling potentially undeterministic behavior,
            // we cheat a little and invoke Stop _before_ invoking ReplayActivityOrStop,
            // just to let ReplayActivityOrStop finish soon.
            // The fact that ReplayActivityOrStop _actually_ blocks until Stop is invoked
            // is verified by another test (WaitsForStopIfNotCompletedAndNotProcessed).
            activityInvocationTracker.Stop();

            activityInvocationTracker.ReplayActivityOrStop(FunctionName, FunctionInput, orchestrationContext, _ => { Assert.True(false, "Unexpected output"); });

            VerifyCallActivityActionAdded(orchestrationContext);

            if (scheduled && completed && !processed)
            {
                VerifyHistoryEventsMarkedAsProcessed(history);
            }
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void WaitsForStopIfNotCompletedAndNotProcessed(bool completedAndNotProcessed, bool expectedWaitForStop)
        {
            var activityInvocationTracker = new ActivityInvocationTracker();

            var history = completedAndNotProcessed
                            ? CreateHistory(scheduled: true, processed: false, completed: true, output: InvocationResultJson)
                            : CreateHistory(scheduled: false, processed: false, completed: false, output: InvocationResultJson);

            var orchestrationContext = new OrchestrationContext { History = history };

            var delayBeforeStopping = TimeSpan.FromSeconds(1);
            var stopwatch = new Stopwatch();

            // ReplayActivityOrStop call may block until Stop is invoked from another thread.
            var thread = new Thread(() =>
                                    {
                                        Thread.Sleep(delayBeforeStopping);
                                        activityInvocationTracker.Stop();
                                    });
            thread.Start();

            stopwatch.Start();
            activityInvocationTracker.ReplayActivityOrStop(FunctionName, FunctionInput, orchestrationContext, _ => { });
            stopwatch.Stop();

            // Check if ReplayActivityOrStop was actually blocked
            if (expectedWaitForStop)
            {
                Assert.True(stopwatch.ElapsedMilliseconds > delayBeforeStopping.TotalMilliseconds * 0.8);
            }
            else
            {
                Assert.True(stopwatch.ElapsedMilliseconds < delayBeforeStopping.TotalMilliseconds * 0.2);
            }

            thread.Join();
        }

        private static HistoryEvent[] CreateHistory(bool scheduled, bool processed, bool completed, string output)
        {
            var history = new List<HistoryEvent>();

            const int taskScheduledEventId = 1;

            if (scheduled)
            {
                history.Add(
                    new HistoryEvent
                    {
                        EventType = HistoryEventType.TaskScheduled,
                        EventId = taskScheduledEventId,
                        Name = FunctionName,
                        IsProcessed = processed
                    });
            }

            if (completed)
            {
                history.Add(
                    new HistoryEvent
                    {
                        EventType = HistoryEventType.TaskCompleted,
                        TaskScheduledId = taskScheduledEventId,
                        Result = output
                    });
            }

            return history.ToArray();
        }

        private static void VerifyCallActivityActionAdded(OrchestrationContext orchestrationContext)
        {
            var actions = GetCollectedActions(orchestrationContext);
            var action = (CallActivityAction) actions.Single().Single();
            Assert.Equal(FunctionName, action.FunctionName);
            Assert.Equal(FunctionInput, action.Input);
        }

        private static List<List<OrchestrationAction>> GetCollectedActions(OrchestrationContext orchestrationContext)
        {
            var (_, actions) = orchestrationContext.OrchestrationActionCollector.WaitForActions(new ManualResetEvent(true));
            return actions;
        }

        private static void VerifyHistoryEventsMarkedAsProcessed(IEnumerable<HistoryEvent> history)
        {
            foreach (var historyEvent in history)
            {
                Assert.True(historyEvent.IsProcessed);
            }
        }
    }
}
