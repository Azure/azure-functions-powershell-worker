//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Durable
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Management.Automation;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable.Tasks;
    using Moq;
    using Xunit;

    public class CurrentUtcDateTimeTests
    {
        private const string FunctionName = "function name";
        private const string FunctionInput = "function input";
        private const string InvocationResultJson = "\"Invocation result\"";
        private static int intervalMilliseconds = 5;
        private static DateTime _startTime = DateTime.UtcNow;
        private static DateTime _restartTime = _startTime.AddMilliseconds(intervalMilliseconds);
        private static DateTime _shouldNotHitTime = _restartTime.AddMilliseconds(intervalMilliseconds);
        private static readonly OrchestrationInvoker _orchestrationInvoker = new OrchestrationInvoker();
        private static OrchestrationBindingInfo _orchestrationBindingInfo = new OrchestrationBindingInfo("ContextParametername",
                                                                                                   new OrchestrationContext());
        private static readonly Mock<IPowerShellServices> _mockPowerShellServices = new Mock<IPowerShellServices>();
        private int _nextEventId = 1;

        // Checks that CurrentUtcDateTime is set/reset to the Timestamp of the first OrchestratorStarted event
        // This test assumes that when OrchestrationInvoker.Invoke() is called, the history contains at least one OrchestratorStarted event
        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void CurrentUtcDateTime_InitializesToFirstOrchestratorStartedTimestamp_IfOrchestratorInvoked(bool firstExecution, bool completed)
        {
            var history = CreateOrchestratorStartedHistory(startTime: _startTime, isProcessed: false);
            var context = new OrchestrationContext { History = history };
            if (!firstExecution)
            {
                // Assumes that a context, when passed to OrchestrationInvoker, has all HistoryEvents' IsProcessed reset to false
                history = DurableTestUtilities.MergeHistories(
                    history,
                    CreateActivityHistory(name: FunctionName,
                                          scheduled: true,
                                          completed: true,
                                          output: InvocationResultJson,
                                          date: _restartTime,
                                          orchestratorStartedIsProcessed: false)
                );
                context.CurrentUtcDateTime =_restartTime;
            }
            _orchestrationBindingInfo = new OrchestrationBindingInfo("ContextParameterName", context);

            InvokeOrchestration(completed: completed);

            Assert.Equal(_orchestrationBindingInfo.Context.History.FirstOrDefault(
                (e) => e.EventType == HistoryEventType.OrchestratorStarted).Timestamp,
                _orchestrationBindingInfo.Context.CurrentUtcDateTime);
        }

        // Verifies that CurrentUtcDateTime updates to the next OrchestratorStarted event (and not a later OrchestratorStartedEvent) if an activity function completes
        // If the activity is not complete, CurrentUtcDateTime does not update
        // This test assumes that when an activity function completes, TaskScheduled -> OrchestratorStarted -> TaskCompleted events are added in that order
        // Otherwise, only a TaskScheduled event is added to history
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CurrentUtcDateTime_UpdatesToNextOrchestratorStartedTimestamp_IfActivityFunctionCompleted(bool completed)
        {
            var history = DurableTestUtilities.MergeHistories(
                CreateOrchestratorStartedHistory(startTime: _startTime, isProcessed: true),
                CreateActivityHistory(
                    name: FunctionName,
                    scheduled: true,
                    completed: completed,
                    output: InvocationResultJson,
                    date: _restartTime,
                    orchestratorStartedIsProcessed: false)
            );

            OrchestrationContext context = new OrchestrationContext { History = history, CurrentUtcDateTime = _startTime };
            var durableTaskHandler = new DurableTaskHandler();
            var allOutput = new List<object>();
            if (completed)
            {
                history = DurableTestUtilities.MergeHistories(history, CreateActivityHistory(
                    name: FunctionName,
                    scheduled: true,
                    completed: true,
                    output: InvocationResultJson,
                    date: _shouldNotHitTime,
                    orchestratorStartedIsProcessed: false));
            }
            else
            {
                DurableTestUtilities.EmulateStop(durableTaskHandler);
            }

            durableTaskHandler.StopAndInitiateDurableTaskOrReplay(
                new ActivityInvocationTask(FunctionName, FunctionInput), context, noWait: false,
                output: output => allOutput.Add(output), onFailure: _ => { });
            if (completed)
            {
                Assert.Equal(_restartTime, context.CurrentUtcDateTime);
            }
            else
            {
                Assert.Equal(_startTime, context.CurrentUtcDateTime);
            }
        }

        // Verifies that in the case of identical Timestamps for consecutive OrchestratorStarted events, CurrentUtcDateTime does not jump ahead
        [Fact]
        public void CurrentUtcDateTime_UpdatesToNextOrchestratorStartedTimestamp_IfTimestampsAreIdentical()
        {
            var history = DurableTestUtilities.MergeHistories(
                CreateActivityHistory(
                    name: FunctionName,
                    scheduled: true,
                    completed: true,
                    date: _startTime,
                    output: InvocationResultJson,
                    orchestratorStartedIsProcessed: true),
                CreateActivityHistory(
                    name: FunctionName,
                    scheduled: true,
                    completed: true,
                    date: _startTime,
                    output: InvocationResultJson,
                    orchestratorStartedIsProcessed: false),
                CreateActivityHistory(
                    name: FunctionName,
                    scheduled: true,
                    completed: true,
                    date: _shouldNotHitTime,
                    output: InvocationResultJson,
                    orchestratorStartedIsProcessed: false)
                    );
            var context = new OrchestrationContext { History = history, CurrentUtcDateTime = _startTime };
            var willHitEvent = context.History.First(
                e => e.EventType == HistoryEventType.OrchestratorStarted &&
                     e.IsProcessed);
            var allOutput = new List<object>();
            var durableTaskHandler = new DurableTaskHandler();

            durableTaskHandler.StopAndInitiateDurableTaskOrReplay(
                new ActivityInvocationTask(FunctionName, FunctionInput), context, noWait: false,
                output: output => allOutput.Add(output), onFailure: _ => { });

            Assert.Equal(_startTime, context.CurrentUtcDateTime);
            var shouldNotHitEvent = context.History.First(
                e => e.Timestamp.Equals(_shouldNotHitTime));
            Assert.True(willHitEvent.IsProcessed);
            Assert.False(shouldNotHitEvent.IsProcessed);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        // Verifies that CurrentUtcDateTime updates to the next OrchestratorStarted event (not a later OrchestratorStartedEvent) if all activity functions complete
        // If any activity is not complete, CurrentUtcDateTime does not update
        // This test assumes that when all activities are completed, TaskScheduled events -> OrchestratorStarted event -> TaskCompleted events are added to history in that order
        // Otherwise, only TaskScheduled events are added to the history 
        public void CurrentUtcDateTime_UpdatesToNextOrchestratorStartedTimestamp_IfAllActivitiesCompleted_WhenWaitAllIsCalled(bool allCompleted)
        {
            var activityFunctions = new Dictionary<string, bool>();
            activityFunctions.Add("FunctionA", true);
            activityFunctions.Add("FunctionB", allCompleted);
            activityFunctions.Add("FunctionC", true);
            var history = DurableTestUtilities.MergeHistories(
                CreateOrchestratorStartedHistory(startTime: _startTime, isProcessed: true),
                CreateNoWaitActivityHistory(scheduled: activityFunctions, restartTime: _restartTime, orchestratorStartedIsProcessed: false),
                CreateOrchestratorStartedHistory(startTime: _shouldNotHitTime, isProcessed: false)
            );
            OrchestrationContext context = new OrchestrationContext { History = history, CurrentUtcDateTime = _startTime };
            var tasksToWaitFor = new ReadOnlyCollection<ActivityInvocationTask>(
                new[] { "FunctionA", "FunctionB", "FunctionC" }.Select(name => new ActivityInvocationTask(name, FunctionInput)).ToArray());
            var durableTaskHandler = new DurableTaskHandler();
            var allOutput = new List<object>();

            if (!allCompleted)
            {
                DurableTestUtilities.EmulateStop(durableTaskHandler);
            }

            durableTaskHandler.WaitAll(tasksToWaitFor, context, output => allOutput.Add(output));
            
            if (allCompleted)
            {
                Assert.Equal(_restartTime, context.CurrentUtcDateTime);
            }
            else
            {
                Assert.Equal(_startTime, context.CurrentUtcDateTime);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CurrentUtcDateTime_UpdatesToNextOrchestratorStartedTimestamp_IfAnyActivitiesCompleted_WhenWaitAnyIsCalled(bool anyCompleted)
        {
            var activityFunctions = new Dictionary<string, bool>();
            activityFunctions.Add("FunctionA", false);
            activityFunctions.Add("FunctionB", anyCompleted);
            activityFunctions.Add("FunctionC", false);
            var history = DurableTestUtilities.MergeHistories(
                CreateOrchestratorStartedHistory(startTime: _startTime, isProcessed: true),
                CreateNoWaitActivityHistory(scheduled: activityFunctions, restartTime: _restartTime, orchestratorStartedIsProcessed: false),
                CreateOrchestratorStartedHistory(startTime: _shouldNotHitTime, isProcessed: false)
            );
            OrchestrationContext context = new OrchestrationContext { History = history, CurrentUtcDateTime = _startTime };
            var tasksToWaitFor = new ReadOnlyCollection<ActivityInvocationTask>(
                new[] { "FunctionA", "FunctionB", "FunctionC" }.Select(name => new ActivityInvocationTask(name, FunctionInput)).ToArray());
            var durableTaskHandler = new DurableTaskHandler();
            var allOutput = new List<object>();

            if (!anyCompleted)
            {
                DurableTestUtilities.EmulateStop(durableTaskHandler);
            }

            durableTaskHandler.WaitAny(tasksToWaitFor, context, output => allOutput.Add(output));
            
            if (anyCompleted)
            {
                Assert.Equal(_restartTime, context.CurrentUtcDateTime);
            }
            else
            {
                Assert.Equal(_startTime, context.CurrentUtcDateTime);
            }
        }

        // Creates a history containing an OrchestratorStarted event
        private HistoryEvent[] CreateOrchestratorStartedHistory(DateTime startTime, bool isProcessed)
        {
            var history = new List<HistoryEvent>();
            // Add an OrchestratorStarted event
            int orchestrationStartedEventId = GetUniqueEventId();

            history.Add(
                new HistoryEvent
                {
                    EventType = HistoryEventType.OrchestratorStarted,
                    EventId = orchestrationStartedEventId,
                    Timestamp = startTime,
                    IsProcessed = isProcessed
                });

            return history.ToArray();
        }

        // Creates a history containing a TaskScheduled and OrchestratorStarted event and/or a TaskCompleted event
        private HistoryEvent[] CreateActivityHistory(
            string name,
            bool scheduled,
            bool completed,
            string output,
            DateTime date,
            bool orchestratorStartedIsProcessed)
        {
            var history = new List<HistoryEvent>();
            
            int taskScheduledEventId = GetUniqueEventId();
            int orchestrationStartedEventId = GetUniqueEventId();

            if (scheduled)
            {
                history.Add(
                    new HistoryEvent
                    {
                        EventType = HistoryEventType.TaskScheduled,
                        EventId = taskScheduledEventId,
                        Name = name
                    });
            }

            if (completed)
            {
                history.Add(
                new HistoryEvent
                {
                    EventType = HistoryEventType.OrchestratorStarted,
                    EventId = orchestrationStartedEventId,
                    Timestamp = date,
                    IsProcessed = orchestratorStartedIsProcessed
                });

                history.Add(
                    new HistoryEvent
                    {
                        EventType = HistoryEventType.TaskCompleted,
                        EventId = GetUniqueEventId(),
                        TaskScheduledId = taskScheduledEventId,
                        Result = output
                    });
            }
            
            return history.ToArray();
        }

        private HistoryEvent[] CreateNoWaitActivityHistory(
            Dictionary<string, bool> scheduled,
            DateTime restartTime,
            bool orchestratorStartedIsProcessed)
        {
            var history = new List<HistoryEvent>();
            var completedEvents = new Dictionary<string, int>();

            // Add scheduled tasks to history
            foreach (string name in scheduled.Keys)
            {
                var taskScheduledEventId = GetUniqueEventId();
                history.Add(
                    new HistoryEvent
                    {
                        EventType = HistoryEventType.TaskScheduled,
                        EventId = taskScheduledEventId,
                        Name = name
                    });

                if ((scheduled[name]))
                {
                    completedEvents.Add(name, taskScheduledEventId);
                }
            }

            var orchestrationStartedEventId = GetUniqueEventId();

            history.Add(
                new HistoryEvent
                {
                    EventType = HistoryEventType.OrchestratorStarted,
                    EventId = orchestrationStartedEventId,
                    Timestamp = restartTime,
                    IsProcessed = orchestratorStartedIsProcessed
                });

            // Add completed tasks to the history
            foreach (string name in completedEvents.Keys)
            {
                history.Add(
                    new HistoryEvent
                    {
                        EventType = HistoryEventType.TaskCompleted,
                        EventId = GetUniqueEventId(),
                        TaskScheduledId = completedEvents[name],
                        Result = InvocationResultJson                        
                    });
            }

            return history.ToArray();
        }

        private int GetUniqueEventId()
        {
            return _nextEventId++;
        }

        private Hashtable InvokeOrchestration(bool completed, PSDataCollection<object> output = null)
        {
            return DurableTestUtilities.InvokeOrchestration(_orchestrationInvoker, _orchestrationBindingInfo, _mockPowerShellServices, completed, output);
        }
    }
}
