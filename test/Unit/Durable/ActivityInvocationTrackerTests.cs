//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Durable
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable;
    using WebJobs.Script.Grpc.Messages;
    using Xunit;

    public class ActivityInvocationTrackerTests
    {
        private const string FunctionName = "function name";
        private const string FunctionInput = "function input";
        private const string InvocationResult = "Invocation result";
        private const string InvocationResultJson = "\"Invocation result\"";

        private const string ActivityTriggerBindingType = "activityTrigger";

        private readonly IEnumerable<AzFunctionInfo> _loadedFunctions =
            new[] { CreateFakeActivityTriggerAzFunctionInfo(FunctionName) };

        private int _nextEventId = 1;

        [Theory]
        [InlineData(true, true)]
        public void ReplayActivityOrStop_ReplaysActivity_IfActivityCompleted(
            bool scheduled, bool completed)
        {
            var history = CreateHistory(scheduled: scheduled, completed: completed, output: InvocationResultJson);
            var orchestrationContext = new OrchestrationContext { History = history };
            var allOutput = new List<object>();

            var activityInvocationTracker = new ActivityInvocationTracker();
            activityInvocationTracker.ReplayActivityOrStop(
                FunctionName, FunctionInput, orchestrationContext, _loadedFunctions, noWait: false,
                output => { allOutput.Add(output); });

            VerifyCallActivityActionAdded(orchestrationContext);
            Assert.Equal(InvocationResult, allOutput.Single());
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void ReplayActivityOrStop_OutputsNothing_IfActivityNotCompleted(
            bool scheduled, bool completed)
        {
            var history = CreateHistory(scheduled: scheduled, completed: completed, output: InvocationResultJson);
            var orchestrationContext = new OrchestrationContext { History = history };

            var activityInvocationTracker = new ActivityInvocationTracker();
            EmulateStop(activityInvocationTracker);

            activityInvocationTracker.ReplayActivityOrStop(
                FunctionName, FunctionInput, orchestrationContext, _loadedFunctions, noWait: false,
                _ => { Assert.True(false, "Unexpected output"); });

            VerifyCallActivityActionAdded(orchestrationContext);
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void ReplayActivityOrStop_WaitsForStop_IfActivityNotCompleted(bool scheduledAndCompleted, bool expectedWaitForStop)
        {
            var activityInvocationTracker = new ActivityInvocationTracker();

            var history = CreateHistory(
                scheduled: scheduledAndCompleted, completed: scheduledAndCompleted, output: InvocationResultJson);

            var orchestrationContext = new OrchestrationContext { History = history };

            VerifyWaitForStop(
                activityInvocationTracker,
                expectedWaitForStop,
                () =>
                {
                    activityInvocationTracker.ReplayActivityOrStop(
                        FunctionName, FunctionInput, orchestrationContext, _loadedFunctions, noWait: false, _ => { });
                });
        }

        [Fact]
        public void ReplayActivityOrStop_ReplaysMultipleActivitiesWithTheSameName()
        {
             var loadedFunctions = new[]
                {
                    CreateFakeActivityTriggerAzFunctionInfo("FunctionA"),
                    CreateFakeActivityTriggerAzFunctionInfo("FunctionB")
                };

            var history = MergeHistories(
                CreateHistory("FunctionA", scheduled: true, completed: true, output: "\"Result1\""),
                CreateHistory("FunctionB", scheduled: true, completed: true, output: "\"Result2\""),
                CreateHistory("FunctionA", scheduled: true, completed: true, output: "\"Result3\"")
            );
            var orchestrationContext = new OrchestrationContext { History = history };
            var allOutput = new List<object>();

            var activityInvocationTracker = new ActivityInvocationTracker();

            // Replay FunctionA only
            for (var i = 0; i < 2; ++i)
            {
                activityInvocationTracker.ReplayActivityOrStop(
                    "FunctionA", FunctionInput, orchestrationContext, loadedFunctions, noWait: false,
                    output => { allOutput.Add(output); });
            }

            // Expect FunctionA results only
            Assert.Equal(new[] { "Result1", "Result3" }, allOutput);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void ReplayActivityOrStop_OutputsActivityTask_WhenNoWaitRequested(
            bool scheduled, bool completed)
        {
            var history = CreateHistory(scheduled: scheduled, completed: completed, output: InvocationResultJson);
            var orchestrationContext = new OrchestrationContext { History = history };
            var allOutput = new List<ActivityInvocationTask>();

            var activityInvocationTracker = new ActivityInvocationTracker();
            activityInvocationTracker.ReplayActivityOrStop(
                FunctionName, FunctionInput, orchestrationContext, _loadedFunctions, noWait: true,
                output => { allOutput.Add((ActivityInvocationTask)output); });

            VerifyCallActivityActionAdded(orchestrationContext);
            Assert.Equal(FunctionName, allOutput.Single().Name);
        }

        [Fact]
        public void ReplayActivityOrStop_Throws_WhenActivityFunctionDoesNotExist()
        {
            var history = CreateHistory(scheduled: false, completed: false, output: InvocationResultJson);
            var orchestrationContext = new OrchestrationContext { History = history };

            var loadedFunctions = new[]
            {
                CreateFakeAzFunctionInfo(FunctionName, "fakeTriggerBindingName", ActivityTriggerBindingType, BindingInfo.Types.Direction.In)
            };

            const string wrongFunctionName = "AnotherFunction";

            var activityInvocationTracker = new ActivityInvocationTracker();

            var exception =
                Assert.Throws<InvalidOperationException>(
                    () => activityInvocationTracker.ReplayActivityOrStop(
                                wrongFunctionName, FunctionInput, orchestrationContext, loadedFunctions, noWait: false,
                                _ => { Assert.True(false, "Unexpected output"); }));

            Assert.Contains(wrongFunctionName, exception.Message);
            Assert.DoesNotContain(ActivityTriggerBindingType, exception.Message);

            VerifyNoCallActivityActionAdded(orchestrationContext);
        }

        [Theory]
        [InlineData("IncorrectBindingType", BindingInfo.Types.Direction.In)]
        [InlineData(ActivityTriggerBindingType, BindingInfo.Types.Direction.Out)]
        public void ReplayActivityOrStop_Throws_WhenActivityFunctionHasNoProperBinding(
            string bindingType, BindingInfo.Types.Direction bindingDirection)
        {
            var history = CreateHistory(scheduled: false, completed: false, output: InvocationResultJson);
            var orchestrationContext = new OrchestrationContext { History = history };

            var loadedFunctions = new[]
            {
                CreateFakeAzFunctionInfo(FunctionName, "fakeTriggerBindingName", bindingType, bindingDirection)
            };

            var activityInvocationTracker = new ActivityInvocationTracker();

            var exception =
                Assert.Throws<InvalidOperationException>(
                    () => activityInvocationTracker.ReplayActivityOrStop(
                                FunctionName, FunctionInput, orchestrationContext, loadedFunctions, noWait: false,
                                _ => { Assert.True(false, "Unexpected output"); }));

            Assert.Contains(FunctionName, exception.Message);
            Assert.Contains(ActivityTriggerBindingType, exception.Message);

            VerifyNoCallActivityActionAdded(orchestrationContext);
        }

        [Theory]
        [InlineData(true, true)]
        public void WaitForActivityTasks_OutputsActivityResults_WhenAllTasksCompleted(
            bool scheduled, bool completed)
        {
            var history = new[]
            {
                // Emulate invoking the same function (FunctionA) twice. This is to make sure that
                // both invocations are accounted for, and both results are preserved separately.
                // Without this test, the history lookup algorithm in the WaitForActivityTasks method
                // could just look for the the first history event by function name, and this error
                // would not be detected.
                CreateHistory("FunctionA", scheduled: scheduled, completed: completed, output: "\"Result1\""),
                CreateHistory("FunctionA", scheduled: scheduled, completed: completed, output: "\"Result2\""),

                CreateHistory("FunctionB", scheduled: scheduled, completed: completed, output: "\"Result3\"")
            }.Aggregate((a, b) => a.Concat(b).ToArray());

            var orchestrationContext = new OrchestrationContext { History = history };
            var tasksToWaitFor =
                new ReadOnlyCollection<ActivityInvocationTask>(
                    new[] { "FunctionA", "FunctionA", "FunctionB" }
                        .Select(name => new ActivityInvocationTask(name))
                        .ToArray());

            var allOutput = new List<object>();

            var activityInvocationTracker = new ActivityInvocationTracker();

            activityInvocationTracker.WaitForActivityTasks(tasksToWaitFor, orchestrationContext, output => { allOutput.Add(output); });

            Assert.Equal(new[] { "Result1", "Result2", "Result3" }, allOutput);
            VerifyNoCallActivityActionAdded(orchestrationContext);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void WaitForActivityTasks_OutputsNothing_WhenAnyTaskIsNotCompleted(
            bool scheduled, bool completed)
        {
            var history = MergeHistories(
                CreateHistory("FunctionA", scheduled: true, completed: true, output: "\"Result1\""), // completed
                CreateHistory("FunctionA", scheduled: scheduled, completed: completed, output: "\"Result2\""),
                CreateHistory("FunctionB", scheduled: true, completed: true, output: "\"Result3\"") // completed
            );

            var orchestrationContext = new OrchestrationContext { History = history };
            var tasksToWaitFor =
                new ReadOnlyCollection<ActivityInvocationTask>(
                    new[] { "FunctionA", "FunctionA", "FunctionB" }
                        .Select(name => new ActivityInvocationTask(name))
                        .ToArray());

            var activityInvocationTracker = new ActivityInvocationTracker();
            EmulateStop(activityInvocationTracker);

            activityInvocationTracker.WaitForActivityTasks(tasksToWaitFor, orchestrationContext,
                                                           _ => { Assert.True(false, "Unexpected output"); });

            VerifyNoCallActivityActionAdded(orchestrationContext);
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void WaitForActivityTasks_WaitsForStop_WhenAnyTaskIsNotCompleted(bool scheduledAndCompleted, bool expectedWaitForStop)
        {
            var history = MergeHistories(
                CreateHistory("FunctionA", scheduled: true, completed: true, output: "\"Result1\""),
                CreateHistory("FunctionA", scheduled: scheduledAndCompleted, completed: scheduledAndCompleted, output: "\"Result2\""),
                CreateHistory("FunctionB", scheduled: true, completed: true, output: "\"Result3\"")
            );

            var activityInvocationTracker = new ActivityInvocationTracker();

            var orchestrationContext = new OrchestrationContext { History = history };
            var tasksToWaitFor =
                new ReadOnlyCollection<ActivityInvocationTask>(
                    new[] { "FunctionA", "FunctionA", "FunctionB" }
                        .Select(name => new ActivityInvocationTask(name))
                        .ToArray());

            VerifyWaitForStop(
                activityInvocationTracker,
                expectedWaitForStop,
                () =>
                {
                    activityInvocationTracker.WaitForActivityTasks(tasksToWaitFor, orchestrationContext, _ => { });
                });
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

        private HistoryEvent[] CreateHistory(bool scheduled, bool completed, string output)
        {
            return CreateHistory(FunctionName, scheduled, completed, output);
        }

        private HistoryEvent[] CreateHistory(string name, bool scheduled, bool completed, string output)
        {
            var history = new List<HistoryEvent>();

            var taskScheduledEventId = GetUniqueEventId();

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
                        EventType = HistoryEventType.TaskCompleted,
                        EventId = GetUniqueEventId(),
                        TaskScheduledId = taskScheduledEventId,
                        Result = output
                    });
            }

            return history.ToArray();
        }

        private static HistoryEvent[] MergeHistories(params HistoryEvent[][] histories)
        {
            return histories.Aggregate((a, b) => a.Concat(b).ToArray());
        }

        private int GetUniqueEventId()
        {
            return _nextEventId++;
        }

        private static void VerifyCallActivityActionAdded(OrchestrationContext orchestrationContext)
        {
            var actions = GetCollectedActions(orchestrationContext);
            var action = (CallActivityAction) actions.Single();
            Assert.Equal(FunctionName, action.FunctionName);
            Assert.Equal(FunctionInput, action.Input);
        }

        private static void VerifyNoCallActivityActionAdded(OrchestrationContext orchestrationContext)
        {
            var actions = GetCollectedActions(orchestrationContext);
            Assert.Empty(actions);
        }

        private static List<OrchestrationAction> GetCollectedActions(OrchestrationContext orchestrationContext)
        {
            var (_, actions) = orchestrationContext.OrchestrationActionCollector.WaitForActions(new ManualResetEvent(true));
            return actions;
        }

        private static void EmulateStop(ActivityInvocationTracker activityInvocationTracker)
        {
            // In the actual usage, Stop is supposed to be invoked from another thread.
            // However, in order to simplify tests and avoid spawning threads, waiting for
            // them to finish, and handling potentially non-deterministic behavior,
            // we cheat a little and invoke Stop _before_ invoking ReplayActivityOrStop/WaitForActivityTasks,
            // just to let ReplayActivityOrStop/WaitForActivityTasks finish soon.
            // The fact that ReplayActivityOrStop/WaitForActivityTasks _actually_ blocks until Stop is invoked
            // is verified by dedicated tests.
            activityInvocationTracker.Stop();
        }

        private static void VerifyWaitForStop(
            ActivityInvocationTracker activityInvocationTracker,
            bool expectedWaitForStop,
            Action action)
        {
            var delayBeforeStopping = TimeSpan.FromSeconds(1);

            // action() call may block until Stop is invoked from another thread.
            var thread = new Thread(() =>
            {
                Thread.Sleep(delayBeforeStopping);
                activityInvocationTracker.Stop();
            });
            thread.Start();
            try
            {
                var elapsedMilliseconds = MeasureExecutionTimeInMilliseconds(action);

                // Check if ReplayActivityOrStop was actually blocked
                if (expectedWaitForStop)
                {
                    Assert.True(elapsedMilliseconds > delayBeforeStopping.TotalMilliseconds * 0.8);
                }
                else
                {
                    Assert.True(elapsedMilliseconds < delayBeforeStopping.TotalMilliseconds * 0.2);
                }
            }
            finally
            {
                thread.Join();
            }
        }

        private static long MeasureExecutionTimeInMilliseconds(Action action)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            action();

            stopwatch.Stop();
            return stopwatch.ElapsedMilliseconds;
        }
    }
}
