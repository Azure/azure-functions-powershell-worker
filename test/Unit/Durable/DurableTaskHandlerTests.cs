//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Durable
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable;
    using Xunit;

    public class DurableTaskHandlerTests
    {
        private const string FunctionInput = "function input";
        private static TimeSpan _delayBeforeStopping = new TimeSpan(0, 0, 2);
        private static TimeSpan _timeInterval = new TimeSpan(0, 0, 1);
        private static readonly DateTime _startTime = DateTime.UtcNow;
        private static readonly DateTime _fireAt = _startTime.Add(_timeInterval);
        private static readonly DateTime _restartTime = _fireAt.Add(_timeInterval);
        private int _nextEventId = 1;

        [Theory]
        [InlineData(true, true)]
        public void WaitAll_OutputsTaskResults_WhenAllTasksCompleted(
            bool scheduled, bool completed)
        {
            var history = DurableTestUtilities.MergeHistories(
                // Emulate invoking the same function (FunctionA) twice. This is to make sure that
                // both invocations are accounted for, and both results are preserved separately.
                // Without this test, the history lookup algorithm in the WaitForActivityTasks method
                // could just look for the the first history event by function name, and this error
                // would not be detected.
                CreateOrchestratorStartedHistory(date: _startTime, isProcessed: true),
                CreateActivityHistory("FunctionA", scheduled: scheduled, completed: completed, output: "\"Result1\""),
                CreateActivityHistory("FunctionA", scheduled: scheduled, completed: completed, output: "\"Result2\""),
                CreateDurableTimerHistory(timerCreated: scheduled, timerFired: completed, fireAt: _fireAt, _restartTime, orchestratorStartedIsProcessed: false),
                CreateActivityHistory("FunctionB", scheduled: scheduled, completed: completed, output: "\"Result3\"")
            );

            var orchestrationContext = new OrchestrationContext { History = history };
            var tasksToWaitFor =
                new ReadOnlyCollection<DurableTask>(
                    new DurableTask[] { 
                        new ActivityInvocationTask("FunctionA", FunctionInput),
                        new ActivityInvocationTask("FunctionA", FunctionInput),
                        new DurableTimerTask(_fireAt),
                        new ActivityInvocationTask("FunctionB", FunctionInput) });

            var allOutput = new List<object>();

            var durableTaskHandler = new DurableTaskHandler();
            durableTaskHandler.WaitAll(tasksToWaitFor, orchestrationContext, output => { allOutput.Add(output); });

            Assert.Equal(new[] { "Result1", "Result2", "Result3" }, allOutput);
            VerifyNoOrchestrationActionAdded(orchestrationContext);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void WaitAll_OutputsNothing_WhenAnyTaskIsNotCompleted(
            bool scheduled, bool completed)
        {
            var history = DurableTestUtilities.MergeHistories(
                CreateActivityHistory("FunctionA", scheduled: true, completed: true, output: "\"Result1\""), // completed
                CreateActivityHistory("FunctionA", scheduled: true, completed: true, output: "\"Result2\""),
                CreateDurableTimerHistory(timerCreated: scheduled, timerFired: completed, fireAt: _fireAt, _restartTime, orchestratorStartedIsProcessed: false),
                CreateActivityHistory("FunctionB", scheduled: true, completed: true, output: "\"Result3\"") // completed
            );

            var orchestrationContext = new OrchestrationContext { History = history };
            var tasksToWaitFor =
                new ReadOnlyCollection<DurableTask>(
                    new DurableTask[] { 
                        new ActivityInvocationTask("FunctionA", FunctionInput),
                        new ActivityInvocationTask("FunctionA", FunctionInput),
                        new DurableTimerTask(_fireAt),
                        new ActivityInvocationTask("FunctionB", FunctionInput) });

            var durableTaskHandler = new DurableTaskHandler();
            DurableTestUtilities.EmulateStop(durableTaskHandler);

            durableTaskHandler.WaitAll(tasksToWaitFor, orchestrationContext,
                                                           _ => { Assert.True(false, "Unexpected output"); });

            VerifyNoOrchestrationActionAdded(orchestrationContext);
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void WaitAll_WaitsForStop_WhenAnyTaskIsNotCompleted(bool scheduledAndCompleted, bool expectedWaitForStop)
        {
            var history = DurableTestUtilities.MergeHistories(
                CreateActivityHistory("FunctionA", scheduled: true, completed: true, output: "\"Result1\""),
                CreateActivityHistory("FunctionA", scheduled: scheduledAndCompleted, completed: scheduledAndCompleted, output: "\"Result2\""),
                CreateDurableTimerHistory(timerCreated: scheduledAndCompleted, timerFired: scheduledAndCompleted, fireAt: _fireAt, _restartTime, orchestratorStartedIsProcessed: false),
                CreateActivityHistory("FunctionB", scheduled: true, completed: true, output: "\"Result3\"")
            );

            var durableTaskHandler = new DurableTaskHandler();

            var orchestrationContext = new OrchestrationContext { History = history };
            var tasksToWaitFor =
                new ReadOnlyCollection<DurableTask>(
                    new DurableTask[] { 
                        new ActivityInvocationTask("FunctionA", FunctionInput),
                        new ActivityInvocationTask("FunctionA", FunctionInput),
                        new DurableTimerTask(_fireAt),
                        new ActivityInvocationTask("FunctionB", FunctionInput) });

            DurableTestUtilities.VerifyWaitForDurableTasks(
                durableTaskHandler,
                _delayBeforeStopping,
                expectedWaitForStop,
                () =>
                {
                    durableTaskHandler.WaitAll(tasksToWaitFor, orchestrationContext, _ => { });
                });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WaitAny_OutputsEarliestCompletedTask_WhenAnyTaskCompleted(bool completed)
        {
            var history = DurableTestUtilities.MergeHistories(
                CreateOrchestratorStartedHistory(date: _startTime, isProcessed: true),
                CreateActivityHistory("FunctionA", scheduled: true, restartTime:_restartTime, completed: completed, output: "\"Result1\"", orchestratorStartedIsProcessed: false),
                CreateActivityHistory("FunctionA", scheduled: false, completed: false, output: "\"Result2\""),
                CreateDurableTimerHistory(timerCreated: true, timerFired: true, fireAt: _fireAt, restartTime: _restartTime, orchestratorStartedIsProcessed: false)
            );

            var orchestrationContext = new OrchestrationContext { History = history };
            var firedTimer = new DurableTimerTask(_fireAt);
            var completedActivity = new ActivityInvocationTask("FunctionA", FunctionInput);
            var tasksToWaitFor =
                new ReadOnlyCollection<DurableTask>(
                    new DurableTask[] { 
                        completedActivity,
                        new ActivityInvocationTask("FunctionA", FunctionInput),
                        firedTimer });

            var allOutput = new List<object>();

            var durableTaskHandler = new DurableTaskHandler();
            durableTaskHandler.WaitAny(tasksToWaitFor, orchestrationContext, output => { allOutput.Add(output); });

            if (completed)
            {
                Assert.Equal(new[] { completedActivity }, allOutput);
            }
            else
            {
                Assert.Equal(new[] { firedTimer }, allOutput);
            }
            VerifyNoOrchestrationActionAdded(orchestrationContext);
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void WaitAny_WaitsForStop_WhenAllTasksAreNotCompleted(bool completed, bool expectedWaitForStop)
        {
            var history = DurableTestUtilities.MergeHistories(
                CreateOrchestratorStartedHistory(date: _startTime, isProcessed: true),
                CreateActivityHistory("FunctionA", scheduled: true, completed: false, output: "\"Result1\""),
                CreateActivityHistory("FunctionA", scheduled: true, completed: completed, output: "\"Result2\"")
            );

            var durableTaskHandler = new DurableTaskHandler();

            var orchestrationContext = new OrchestrationContext { History = history };
            var tasksToWaitFor =
                new ReadOnlyCollection<DurableTask>(
                    new DurableTask[] { 
                        new ActivityInvocationTask("FunctionA", FunctionInput),
                        new ActivityInvocationTask("FunctionA", FunctionInput) });

            DurableTestUtilities.VerifyWaitForDurableTasks(
                durableTaskHandler,
                _delayBeforeStopping,
                expectedWaitForStop,
                () =>
                {
                    durableTaskHandler.WaitAny(tasksToWaitFor, orchestrationContext, _ => { });
                });
        }

        private HistoryEvent[] CreateActivityHistory(string name, bool scheduled, bool completed, string output) {
            return CreateActivityHistory(name: name, scheduled: scheduled, restartTime: _restartTime, completed: completed, output: output, orchestratorStartedIsProcessed: false);
        }

        private HistoryEvent[] CreateActivityHistory(string name, bool scheduled, DateTime restartTime, bool completed, string output, bool orchestratorStartedIsProcessed)
        {
            var history = new List<HistoryEvent>();

            if (scheduled)
            {
                var taskScheduledEventId = GetUniqueEventId();

                history.Add(
                    new HistoryEvent
                    {
                        EventType = HistoryEventType.TaskScheduled,
                        EventId = taskScheduledEventId,
                        Name = name
                    });

                var orchestratorStartedEventId = GetUniqueEventId();
                history.Add(
                    new HistoryEvent
                    {
                        EventType = HistoryEventType.OrchestratorStarted,
                        Timestamp = restartTime,
                        IsProcessed = orchestratorStartedIsProcessed
                    });

                if (completed)
                {
                    history.Add(
                        new HistoryEvent
                        {
                            EventType = HistoryEventType.TaskCompleted,
                            EventId = GetUniqueEventId(),
                            TaskScheduledId = taskScheduledEventId,
                            Result = output
                        });
                }
            }

            return history.ToArray();
        }

        private HistoryEvent[] CreateDurableTimerHistory(bool timerCreated, bool timerFired, DateTime fireAt, DateTime restartTime, bool orchestratorStartedIsProcessed)
        {
            var history = new List<HistoryEvent>();

            if (timerCreated) {
                int timerCreatedEventId = GetUniqueEventId();
                history.Add(
                    new HistoryEvent
                    {
                        EventType = HistoryEventType.TimerCreated,
                        EventId = timerCreatedEventId,
                        FireAt = fireAt
                    }
                );

                int orchestratorStartEventId = GetUniqueEventId();
                history.Add(
                    new HistoryEvent
                    {
                        EventType = HistoryEventType.OrchestratorStarted,
                        EventId = orchestratorStartEventId,
                        Timestamp = restartTime,
                        IsProcessed = orchestratorStartedIsProcessed
                    }
                );

                if (timerFired)
                {
                    history.Add(
                        new HistoryEvent
                        {
                            EventType = HistoryEventType.TimerFired,
                            EventId = GetUniqueEventId(),
                            TimerId = timerCreatedEventId,
                            FireAt = fireAt
                        }
                    );
                }
            }

            return history.ToArray();
        }

        private HistoryEvent[] CreateOrchestratorStartedHistory(DateTime date, bool isProcessed)
        {
            var history = new List<HistoryEvent>();
            
            int orchestratorStartEventId = GetUniqueEventId();

            history.Add(
                new HistoryEvent
                {
                    EventType = HistoryEventType.OrchestratorStarted,
                    EventId = orchestratorStartEventId,
                    Timestamp = date,
                    IsProcessed = isProcessed
                }
            );

            return history.ToArray();
        }

        private int GetUniqueEventId()
        {
            return _nextEventId++;
        }

        private static void VerifyNoOrchestrationActionAdded(OrchestrationContext orchestrationContext)
        {
            var actions = DurableTestUtilities.GetCollectedActions(orchestrationContext);
            Assert.Empty(actions);
        }

    }
}
