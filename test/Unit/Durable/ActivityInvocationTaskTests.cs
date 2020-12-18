//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Durable
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable;
    using WebJobs.Script.Grpc.Messages;
    using Xunit;

    public class ActivityInvocationTaskTests
    {
        private const string FunctionName = "function name";
        private const string FunctionInput = "function input";
        private const string InvocationResult = "Invocation result";
        private const string InvocationResultJson = "\"Invocation result\"";

        private const string ActivityTriggerBindingType = "activityTrigger";
        
        private static TimeSpan _delayBeforeStopping = new TimeSpan(0, 0, 1);

        private int _nextEventId = 1;

        [Theory]
        [InlineData(true, true)]
        public void StopAndInitiateDurableTaskOrReplay_ReplaysActivity_IfActivityCompleted(
            bool scheduled, bool completed)
        {
            var history = CreateHistory(scheduled: scheduled, completed: completed, failed: false, output: InvocationResultJson);
            var orchestrationContext = new OrchestrationContext { History = history };
            var allOutput = new List<object>();

            var durableTaskHandler = new DurableTaskHandler();
            durableTaskHandler.StopAndInitiateDurableTaskOrReplay(
                task: new ActivityInvocationTask(FunctionName, FunctionInput), orchestrationContext, noWait: false,
                output: output => { allOutput.Add(output); },
                onFailure: reason => { Assert.True(false, $"Unexpected failure: {reason}"); });

            VerifyCallActivityActionAdded(orchestrationContext);
            Assert.Equal(InvocationResult, allOutput.Single());
        }

        [Fact]
        public void StopAndInitiateDurableTaskOrReplay_OutputsError_IfActivityFailed()
        {
            const string FailureReason = "Failure reason";
            var history = CreateHistory(scheduled: true, completed: false, failed: true, output: InvocationResultJson, failureReason: FailureReason);
            var orchestrationContext = new OrchestrationContext { History = history };
            var allErrors = new List<object>();

            var durableTaskHandler = new DurableTaskHandler();
            durableTaskHandler.StopAndInitiateDurableTaskOrReplay(
                task: new ActivityInvocationTask(FunctionName, FunctionInput), orchestrationContext, noWait: false,
                output: _ => { Assert.True(false, "Unexpected output"); },
                onFailure: reason => { allErrors.Add(reason); });

            VerifyCallActivityActionAdded(orchestrationContext);
            Assert.Equal(FailureReason, allErrors.Single());
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void StopAndInitiateDurableTaskOrReplay_OutputsNothing_IfActivityNotCompleted(
            bool scheduled, bool completed)
        {
            var history = CreateHistory(scheduled: scheduled, completed: completed, failed: false, output: InvocationResultJson);
            var orchestrationContext = new OrchestrationContext { History = history };

            var durableTaskHandler = new DurableTaskHandler();
            DurableTestUtilities.EmulateStop(durableTaskHandler);

            durableTaskHandler.StopAndInitiateDurableTaskOrReplay(
                new ActivityInvocationTask(FunctionName, FunctionInput), orchestrationContext, noWait: false,
                output: _ => { Assert.True(false, "Unexpected output"); },
                onFailure: reason => { Assert.True(false, $"Unexpected failure: {reason}"); });

            VerifyCallActivityActionAdded(orchestrationContext);
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void StopAndInitiateDurableTaskOrReplay_WaitsForStop_IfActivityNotCompleted(bool scheduledAndCompleted, bool expectedWaitForStop)
        {
            var durableTaskHandler = new DurableTaskHandler();

            var history = CreateHistory(
                scheduled: scheduledAndCompleted, completed: scheduledAndCompleted, failed: false, output: InvocationResultJson);

            var orchestrationContext = new OrchestrationContext { History = history };

            DurableTestUtilities.VerifyWaitForDurableTasks(
                durableTaskHandler,
                _delayBeforeStopping,
                expectedWaitForStop,
                () =>
                {
                    durableTaskHandler.StopAndInitiateDurableTaskOrReplay(
                        new ActivityInvocationTask(FunctionName, FunctionInput), orchestrationContext, noWait: false,
                        output: _ => { }, onFailure: _ => { });
                });
        }

        [Fact]
        public void StopAndInitiateDurableTaskOrReplay_ReplaysMultipleActivitiesWithTheSameName()
        {
             var loadedFunctions = new[]
                {
                    DurableTestUtilities.CreateFakeActivityTriggerAzFunctionInfo("FunctionA"),
                    DurableTestUtilities.CreateFakeActivityTriggerAzFunctionInfo("FunctionB")
                };

            var history = DurableTestUtilities.MergeHistories(
                CreateHistory("FunctionA", scheduled: true, completed: true, failed: false, output: "\"Result1\""),
                CreateHistory("FunctionB", scheduled: true, completed: true, failed: false, output: "\"Result2\""),
                CreateHistory("FunctionA", scheduled: true, completed: true, failed: false, output: "\"Result3\"")
            );
            var orchestrationContext = new OrchestrationContext { History = history };
            var allOutput = new List<object>();

            var durableTaskHandler = new DurableTaskHandler();

            // Replay FunctionA only
            for (var i = 0; i < 2; ++i)
            {
                durableTaskHandler.StopAndInitiateDurableTaskOrReplay(
                    new ActivityInvocationTask("FunctionA", FunctionInput), orchestrationContext, noWait: false,
                    output: output => { allOutput.Add(output); },
                    onFailure: _ => { });
            }

            // Expect FunctionA results only
            Assert.Equal(new[] { "Result1", "Result3" }, allOutput);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void StopAndInitiateDurableTaskOrReplay_OutputsActivityInvocationTask_WhenNoWaitRequested(
            bool scheduled, bool completed)
        {
            var history = CreateHistory(scheduled: scheduled, completed: completed, failed: false, output: InvocationResultJson);
            var orchestrationContext = new OrchestrationContext { History = history };
            var allOutput = new List<ActivityInvocationTask>();

            var durableTaskHandler = new DurableTaskHandler();
            durableTaskHandler.StopAndInitiateDurableTaskOrReplay(
                new ActivityInvocationTask(FunctionName, FunctionInput), orchestrationContext, noWait: true,
                output: output => { allOutput.Add((ActivityInvocationTask)output); },
                onFailure: _ => { });

            VerifyCallActivityActionAdded(orchestrationContext);
            Assert.Equal(FunctionName, allOutput.Single().FunctionName);
        }

        [Fact]
        public void ValidateTask_Throws_WhenActivityFunctionDoesNotExist()
        {
            var history = CreateHistory(scheduled: false, completed: false, failed: false, output: InvocationResultJson);
            var orchestrationContext = new OrchestrationContext { History = history };

            var loadedFunctions = new[]
            {
                DurableTestUtilities.CreateFakeAzFunctionInfo(FunctionName, "fakeTriggerBindingName", ActivityTriggerBindingType, BindingInfo.Types.Direction.In)
            };

            const string wrongFunctionName = "AnotherFunction";

            var durableTaskHandler = new DurableTaskHandler();

            var exception =
                Assert.Throws<InvalidOperationException>(
                    () => ActivityInvocationTask.ValidateTask(
                                new ActivityInvocationTask(wrongFunctionName, FunctionInput), loadedFunctions));

            Assert.Contains(wrongFunctionName, exception.Message);
            Assert.DoesNotContain(ActivityTriggerBindingType, exception.Message);

            DurableTestUtilities.VerifyNoActionAdded(orchestrationContext);
        }

        [Theory]
        [InlineData("IncorrectBindingType", BindingInfo.Types.Direction.In)]
        [InlineData(ActivityTriggerBindingType, BindingInfo.Types.Direction.Out)]
        public void ValidateTask_Throws_WhenActivityFunctionHasNoProperBinding(
            string bindingType, BindingInfo.Types.Direction bindingDirection)
        {
            var history = CreateHistory(scheduled: false, completed: false, failed: false, output: InvocationResultJson);
            var orchestrationContext = new OrchestrationContext { History = history };

            var loadedFunctions = new[]
            {
                DurableTestUtilities.CreateFakeAzFunctionInfo(FunctionName, "fakeTriggerBindingName", bindingType, bindingDirection)
            };

            var durableTaskHandler = new DurableTaskHandler();

            var exception =
                Assert.Throws<InvalidOperationException>(
                    () => ActivityInvocationTask.ValidateTask(
                                new ActivityInvocationTask(FunctionName, FunctionInput), loadedFunctions));

            Assert.Contains(FunctionName, exception.Message);
            Assert.Contains(ActivityTriggerBindingType, exception.Message);

            DurableTestUtilities.VerifyNoActionAdded(orchestrationContext);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GetCompletedHistoryEvent_ReturnsTaskCompletedOrTaskFailed(bool succeeded)
        {
            var history = CreateHistory(scheduled: true, completed: succeeded, failed: !succeeded, output: InvocationResultJson);
            var orchestrationContext = new OrchestrationContext { History = history };

            var task = new ActivityInvocationTask(FunctionName, FunctionInput);
            var scheduledEvent = task.GetScheduledHistoryEvent(orchestrationContext);
            var completedEvent = task.GetCompletedHistoryEvent(orchestrationContext, scheduledEvent);

            Assert.Equal(scheduledEvent.EventId, completedEvent.TaskScheduledId);
        }

        private HistoryEvent[] CreateHistory(bool scheduled, bool completed, bool failed, string output, string failureReason = null)
        {
            return CreateHistory(FunctionName, scheduled, completed, failed, output, failureReason);
        }

        private HistoryEvent[] CreateHistory(string name, bool scheduled, bool completed, bool failed, string output, string failureReason = null)
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

            if (failed)
            {
                history.Add(
                    new HistoryEvent
                    {
                        EventType = HistoryEventType.TaskFailed,
                        EventId = GetUniqueEventId(),
                        TaskScheduledId = taskScheduledEventId,
                        Result = output,
                        Reason = failureReason
                    });
            }

            return history.ToArray();
        }

        private int GetUniqueEventId()
        {
            return _nextEventId++;
        }

        private static void VerifyCallActivityActionAdded(OrchestrationContext orchestrationContext)
        {
            var actions = DurableTestUtilities.GetCollectedActions(orchestrationContext);
            var action = (CallActivityAction) actions.Single();
            Assert.Equal(FunctionName, action.FunctionName);
            Assert.Equal(FunctionInput, action.Input);
        }
    }
}
