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
    using System.Diagnostics;
    using System.Linq;
    using System.Management.Automation;
    using System.Threading;
    using Moq;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable;
    using WebJobs.Script.Grpc.Messages;
    using Xunit;

    internal static class DurableTestUtilities
    {
        public static AzFunctionInfo CreateFakeActivityTriggerAzFunctionInfo(string functionName)
        {
            return CreateFakeAzFunctionInfo(functionName, "fakeTriggerBindingName", "activityTrigger", BindingInfo.Types.Direction.In);
        }

        public static AzFunctionInfo CreateFakeAzFunctionInfo(
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

        public static IAsyncResult CreateInvocationResult(bool completed)
        {
            var completionEvent = new AutoResetEvent(initialState: completed);
            var result = new Mock<IAsyncResult>();
            result.Setup(_ => _.AsyncWaitHandle).Returns(completionEvent);
            return result.Object;
        }

        public static void EmulateStop(DurableTaskHandler durableTaskHandler)
        {
            // In the actual usage, Stop is supposed to be invoked from another thread.
            // However, in order to simplify tests and avoid spawning threads, waiting for
            // them to finish, and handling potentially non-deterministic behavior,
            // we cheat a little and invoke Stop _before_ invoking WaitAll/WaitAny,
            // just to let WaitAll/WaitAny finish soon.
            // The fact that WaitAll/WaitAny _actually_ blocks until Stop is invoked
            // is verified by dedicated tests.
            durableTaskHandler.Stop();
        }

        public static void ExpectBeginInvoke(
            Mock<IPowerShellServices> mockPowerShellServices,
            IAsyncResult invocationAsyncResult,
            PSDataCollection<object> output = null)
        {
            mockPowerShellServices
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

        public static List<OrchestrationAction> GetCollectedActions(OrchestrationContext orchestrationContext)
        {
            var (_, actions) = orchestrationContext.OrchestrationActionCollector.WaitForActions(new ManualResetEvent(true));
            return actions;
        }

        public static Hashtable InvokeOrchestration(
            OrchestrationInvoker orchestrationInvoker,
            OrchestrationBindingInfo orchestrationBindingInfo,
            Mock<IPowerShellServices> mockPowerShellServices,
            bool completed,
            PSDataCollection<object> output = null)
        {
            var invocationAsyncResult = CreateInvocationResult(completed);
            ExpectBeginInvoke(mockPowerShellServices, invocationAsyncResult, output);
            if (!completed)
            {
                SignalToStopInvocation(orchestrationBindingInfo);
            }

            var result = orchestrationInvoker.Invoke(orchestrationBindingInfo, mockPowerShellServices.Object);
            return result;
        }

        public static HistoryEvent[] MergeHistories(params HistoryEvent[][] histories)
        {
            return histories.Aggregate((a, b) => a.Concat(b).ToArray());
        }

        public static long MeasureExecutionTimeInMilliseconds(Action action)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            action();

            stopwatch.Stop();
            return stopwatch.ElapsedMilliseconds;
        }

        public static void SignalToStopInvocation(OrchestrationBindingInfo orchestrationBindingInfo)
        {
            orchestrationBindingInfo.Context.OrchestrationActionCollector.Stop();
        }

        public static void VerifyNoActionAdded(OrchestrationContext orchestrationContext)
        {
            var actions = DurableTestUtilities.GetCollectedActions(orchestrationContext);
            Assert.Empty(actions);
        }

        public static void VerifyWaitForDurableTasks(
            DurableTaskHandler durableTaskHandler,
            TimeSpan delayBeforeStopping,
            bool expectedWaitForStop,
            Action action
        )
        {
            // action() call may block until Stop is invoked from another thread.
            var thread = new Thread(() =>
            {
                Thread.Sleep(delayBeforeStopping);
                durableTaskHandler.Stop();
            });
            thread.Start();
            try
            {
                var elapsedMilliseconds = MeasureExecutionTimeInMilliseconds(action);

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
    }
}
