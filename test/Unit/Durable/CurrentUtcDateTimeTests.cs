//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Durable
{
    using System;
    using System.Threading;
    using System.Management.Automation;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable;
    using Moq;
    using WebJobs.Script.Grpc.Messages;
    using Xunit;

    public class CurrentUtcDateTimeTests
    {
        private const string FunctionName = "function name";
        private const string FunctionInput = "function input";
        private const string ActivityTriggerBindingType = "activityTrigger";
        private const string InvocationResultJson = "\"Invocation result\"";
        private const DateTimeKind utc = DateTimeKind.Utc;
        private DateTime time = new DateTime(2020, 1, 1, 0, 0, 0, 0, utc);
        private int intervalMilliseconds = 5;
        private int _nextEventId = 1;
        private readonly IEnumerable<AzFunctionInfo> _loadedFunctions =
            new[] { CreateFakeActivityTriggerAzFunctionInfo(FunctionName) };

        readonly OrchestrationInvoker _orchestrationInvoker = new OrchestrationInvoker();

        private OrchestrationBindingInfo _orchestrationBindingInfo = new OrchestrationBindingInfo("ContextParametername",
                                                                                                   new OrchestrationContext());

        private readonly Mock<IPowerShellServices> _mockPowerShellServices = new Mock<IPowerShellServices>();

        // Checks that CurrentUtcDateTime is set/reset to the Timestamp of the first OrchestratorStarted event
        // This test assumes that when OrchestrationInvoker.Invoke() is called, the history contains at least one OrchestratorStarted event
        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void CurrentUtcDateTime_InitializesToFirstOrchestratorStartedTimestamp_IfOrchestratorInvoked(bool firstExecution, bool completed)
        {
            if (firstExecution)
            {
                _orchestrationBindingInfo = new OrchestrationBindingInfo(
                    "ContextParameterName",
                    new OrchestrationContext { History = CreateOrchestratorStartedHistory(date: time, isProcessed: false) }
                    );
            }
            else
            {
                DateTime startTime = time;
                DateTime restartTime = startTime.AddMilliseconds(intervalMilliseconds);
                // Assumes that a context, when passed to OrchestrationInvoker, has all HistoryEvents' IsProcessed reset to false
                var history = MergeHistories(
                    CreateOrchestratorStartedHistory(date: time, isProcessed: false),
                    CreateActivityHistory(name: FunctionName,
                                          scheduled: true,
                                          completed: true,
                                          output: InvocationResultJson,
                                          date: restartTime,
                                          orchestratorStartedIsProcessed: false)
                );
                var context = new OrchestrationContext { History = history, CurrentUtcDateTime = restartTime };
                _orchestrationBindingInfo = new OrchestrationBindingInfo("ContextParameterName", context);
            }

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
            DateTime startTime = time;
            DateTime restartTime = startTime.AddMilliseconds(intervalMilliseconds);
            DateTime shouldNotHitTime = restartTime.AddMilliseconds(intervalMilliseconds);
            var history = MergeHistories(
                CreateOrchestratorStartedHistory(date: startTime, isProcessed: true),
                CreateActivityHistory(
                    name: FunctionName,
                    scheduled: true,
                    completed: completed,
                    output: InvocationResultJson,
                    date: restartTime,
                    orchestratorStartedIsProcessed: false)
            );

            var _activityInvocationTracker = new ActivityInvocationTracker();
            if (completed)
            {
                history = MergeHistories(history, CreateActivityHistory(
                    name: FunctionName,
                    scheduled: true,
                    completed: true,
                    output: InvocationResultJson,
                    date: shouldNotHitTime,
                    orchestratorStartedIsProcessed: false));
            }
            else
            {
                EmulateStop(_activityInvocationTracker);
            }

            OrchestrationContext context = new OrchestrationContext { History = history, CurrentUtcDateTime = startTime };
            var allOutput = new List<object>();

            _activityInvocationTracker.ReplayActivityOrStop(FunctionName, FunctionInput, context, _loadedFunctions, noWait: false, output => allOutput.Add(output));
            if (completed)
            {
                Assert.Equal(restartTime, context.CurrentUtcDateTime);
            }
            else
            {
                Assert.Equal(startTime, context.CurrentUtcDateTime);
            }
        }

        // Verifies that in the case of identical Timestamps for consecutive OrchestratorStarted events, CurrentUtcDateTime does not jump ahead
        [Fact]
        public void CurrentUtcDateTime_UpdatesToNextOrchestratorStartedTimestamp_IfTimestampsAreIdentical()
        {
            DateTime startTime = time;
            DateTime shouldNotHitTime = startTime.AddMilliseconds(intervalMilliseconds);
            var history = MergeHistories(
                CreateActivityHistory(
                    name: FunctionName,
                    scheduled: true,
                    completed: true,
                    date: startTime,
                    output: InvocationResultJson,
                    orchestratorStartedIsProcessed: true),
                CreateActivityHistory(
                    name: FunctionName,
                    scheduled: true,
                    completed: true,
                    date: startTime,
                    output: InvocationResultJson,
                    orchestratorStartedIsProcessed: false),
                CreateActivityHistory(
                    name: FunctionName,
                    scheduled: true,
                    completed: true,
                    date: shouldNotHitTime,
                    output: InvocationResultJson,
                    orchestratorStartedIsProcessed: false)
                    );
            var context = new OrchestrationContext { History = history, CurrentUtcDateTime = startTime };
            var willHitEvent = context.History.First(
                e => e.EventType == HistoryEventType.OrchestratorStarted &&
                     e.IsProcessed);
            var allOutput = new List<object>();
            var _activityInvocationTracker = new ActivityInvocationTracker();

            _activityInvocationTracker.ReplayActivityOrStop(FunctionName, FunctionInput, context, _loadedFunctions, noWait: false, output => allOutput.Add(output));

            Assert.Equal(startTime, context.CurrentUtcDateTime);
            var shouldNotHitEvent = context.History.First(
                e => e.Timestamp.Equals(shouldNotHitTime));
            Assert.Equal(true, willHitEvent.IsProcessed);
            Assert.Equal(false, shouldNotHitEvent.IsProcessed);
        }


        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        // Verifies that CurrentUtcDateTime updates to the next OrchestratorStarted event (not a later OrchestratorStartedEvent) if all activity functions complete
        // If any activity is not complete, CurrentUtcDateTime does not update
        // This test assumes that when all activities are completed, TaskScheduled events -> OrchestratorStarted event -> TaskCompleted events are added to history in that order
        // Otherwise, only TaskScheduled events are added to the history 
        public void CurrentUtcDateTime_UpdatesToNextOrchestratorStartedTimestamp_IfAllActivitiesCompleted(bool allCompleted)
        {
            DateTime startTime = time;
            DateTime restartTime = startTime.AddMilliseconds(intervalMilliseconds);
            DateTime shouldNotHitTime = restartTime.AddMilliseconds(intervalMilliseconds);
            var activityFunctions = new Dictionary<string, bool>();
            activityFunctions.Add("FunctionA", true);
            activityFunctions.Add("FunctionB", allCompleted);
            activityFunctions.Add("FunctionC", true);
            var history = MergeHistories(
                CreateOrchestratorStartedHistory(date: startTime, isProcessed: true),
                CreateNoWaitActivityHistory(scheduled: activityFunctions, restartTime: restartTime, orchestratorStartedIsProcessed: false),
                CreateOrchestratorStartedHistory(date: shouldNotHitTime, isProcessed: false)
            );
            OrchestrationContext context = new OrchestrationContext { History = history, CurrentUtcDateTime = startTime };
            var tasksToWaitFor = new ReadOnlyCollection<ActivityInvocationTask>(
                new[] { "FunctionA", "FunctionB", "FunctionC" }.Select(name => new ActivityInvocationTask(name: name)).ToArray());
            var _activityInvocationTracker = new ActivityInvocationTracker();
            var allOutput = new List<object>();

            if (!allCompleted)
            {
                EmulateStop(_activityInvocationTracker);
            }

            _activityInvocationTracker.WaitForActivityTasks(tasksToWaitFor, context, output => allOutput.Add(output));
            
            if (allCompleted)
            {
                Assert.Equal(restartTime, context.CurrentUtcDateTime);
            }
            else
            {
                Assert.Equal(startTime, context.CurrentUtcDateTime);
            }
        }

        // Creates a history containing an OrchestratorStarted event
        private HistoryEvent[] CreateOrchestratorStartedHistory(DateTime date, bool isProcessed)
        {
            var history = new List<HistoryEvent>();
            // Add an OrchestratorStarted event
            int orchestrationStartedEventId = GetUniqueEventId();

            history.Add(
                new HistoryEvent
                {
                    EventType = HistoryEventType.OrchestratorStarted,
                    EventId = orchestrationStartedEventId,
                    Timestamp = date,
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

            // Add completed tasks to the history if all completed
            if (completedEvents.Keys.Count == scheduled.Keys.Count)
            {
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
            }

            return history.ToArray();
        }

        private static HistoryEvent[] MergeHistories(params HistoryEvent[][] histories)
        {
            return histories.Aggregate((a, b) => a.Concat(b).ToArray());
        }

        private static AzFunctionInfo CreateFakeActivityTriggerAzFunctionInfo(string functionName)
        {
            return CreateFakeAzFunctionInfo(functionName, "fakeTriggerBindingName", ActivityTriggerBindingType, BindingInfo.Types.Direction.In);
        }

        private static AzFunctionInfo CreateFakeAzFunctionInfo(
            string functionName,
            string bindingName,
            string bindingType,
            BindingInfo.Types.Direction bindingDirection)
        {
            return new AzFunctionInfo(
                functionName,
                new ReadOnlyDictionary<string, ReadOnlyBindingInfo>(
                    new Dictionary<string, ReadOnlyBindingInfo>
                    {
                        {
                            bindingName,
                            new ReadOnlyBindingInfo(bindingType, bindingDirection)
                        }
                    }));
        }

        private int GetUniqueEventId()
        {
            return _nextEventId++;
        }

        private Hashtable InvokeOrchestration(bool completed, PSDataCollection<object> output = null)
        {
            var invocationAsyncResult = CreateInvocationResult(completed);
            ExpectBeginInvoke(invocationAsyncResult, output);
            if (!completed)
            {
                SignalToStopInvocation();
            }

            var result = _orchestrationInvoker.Invoke(_orchestrationBindingInfo, _mockPowerShellServices.Object);
            return result;
        }

        private static IAsyncResult CreateInvocationResult(bool completed)
        {
            var completionEvent = new AutoResetEvent(initialState: completed);
            var result = new Mock<IAsyncResult>();
            result.Setup(_ => _.AsyncWaitHandle).Returns(completionEvent);
            return result.Object;
        }

        private void EmulateStop(ActivityInvocationTracker activityInvocationTracker)
        {
            activityInvocationTracker.Stop();
        }

        private void ExpectBeginInvoke(IAsyncResult invocationAsyncResult, PSDataCollection<object> output = null)
        {
            _mockPowerShellServices
                .Setup(_ => _.BeginInvoke(It.IsAny<PSDataCollection<object>>()))
                .Returns(
                    (PSDataCollection<object> outputBuffer) =>
                    {
                        if (output != null)
                        {
                            foreach (var item in output)
                            {
                                outputBuffer.Add(item);
                            }
                        }

                        return invocationAsyncResult;
                    });
        }

        private void SignalToStopInvocation()
        {
            _orchestrationBindingInfo.Context.OrchestrationActionCollector.Stop();
        }
    }
}
