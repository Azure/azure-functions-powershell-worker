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
            var history = CreateHistory(scheduled: scheduled, completed: completed, output: InvocationResultJson);
            var orchestrationContext = new OrchestrationContext { History = history };
            var allOutput = new List<object>();

            var durableTaskHandler = new DurableTaskHandler();
            durableTaskHandler.StopAndInitiateDurableTaskOrReplay(
                task: new ActivityInvocationTask(FunctionName, FunctionInput), orchestrationContext, noWait: false,
                output => { allOutput.Add(output); });

            VerifyCallActivityActionAdded(orchestrationContext);
            Assert.Equal(InvocationResult, allOutput.Single());
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void StopAndInitiateDurableTaskOrReplay_OutputsNothing_IfActivityNotCompleted(
            bool scheduled, bool completed)
        {
            var history = CreateHistory(scheduled: scheduled, completed: completed, output: InvocationResultJson);
            var orchestrationContext = new OrchestrationContext { History = history };

            var durableTaskHandler = new DurableTaskHandler();
            DurableTestUtilities.EmulateStop(durableTaskHandler);

            durableTaskHandler.StopAndInitiateDurableTaskOrReplay(
                new ActivityInvocationTask(FunctionName, FunctionInput), orchestrationContext, noWait: false,
                _ => { Assert.True(false, "Unexpected output"); });

            VerifyCallActivityActionAdded(orchestrationContext);
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void StopAndInitiateDurableTaskOrReplay_WaitsForStop_IfActivityNotCompleted(bool scheduledAndCompleted, bool expectedWaitForStop)
        {
            var durableTaskHandler = new DurableTaskHandler();

            var history = CreateHistory(
                scheduled: scheduledAndCompleted, completed: scheduledAndCompleted, output: InvocationResultJson);

            var orchestrationContext = new OrchestrationContext { History = history };

            DurableTestUtilities.VerifyWaitForDurableTasks(
                durableTaskHandler,
                _delayBeforeStopping,
                expectedWaitForStop,
                () =>
                {
                    durableTaskHandler.StopAndInitiateDurableTaskOrReplay(
                        new ActivityInvocationTask(FunctionName, FunctionInput), orchestrationContext, noWait: false, _ => { });
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
                CreateHistory("FunctionA", scheduled: true, completed: true, output: "\"Result1\""),
                CreateHistory("FunctionB", scheduled: true, completed: true, output: "\"Result2\""),
                CreateHistory("FunctionA", scheduled: true, completed: true, output: "\"Result3\"")
            );
            var orchestrationContext = new OrchestrationContext { History = history };
            var allOutput = new List<object>();

            var durableTaskHandler = new DurableTaskHandler();

            // Replay FunctionA only
            for (var i = 0; i < 2; ++i)
            {
                durableTaskHandler.StopAndInitiateDurableTaskOrReplay(
                    new ActivityInvocationTask("FunctionA", FunctionInput), orchestrationContext, noWait: false,
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
        public void StopAndInitiateDurableTaskOrReplay_OutputsActivityInvocationTask_WhenNoWaitRequested(
            bool scheduled, bool completed)
        {
            var history = CreateHistory(scheduled: scheduled, completed: completed, output: InvocationResultJson);
            var orchestrationContext = new OrchestrationContext { History = history };
            var allOutput = new List<ActivityInvocationTask>();

            var durableTaskHandler = new DurableTaskHandler();
            durableTaskHandler.StopAndInitiateDurableTaskOrReplay(
                new ActivityInvocationTask(FunctionName, FunctionInput), orchestrationContext, noWait: true,
                output => { allOutput.Add((ActivityInvocationTask)output); });

            VerifyCallActivityActionAdded(orchestrationContext);
            Assert.Equal(FunctionName, allOutput.Single().FunctionName);
        }

        [Fact]
        public void ValidateTask_Throws_WhenActivityFunctionDoesNotExist()
        {
            var history = CreateHistory(scheduled: false, completed: false, output: InvocationResultJson);
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
            var history = CreateHistory(scheduled: false, completed: false, output: InvocationResultJson);
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
