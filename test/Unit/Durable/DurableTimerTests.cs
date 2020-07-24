//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Durable
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Management.Automation;
    using System.Threading;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable;
    using Moq;
    using Xunit;

    public class DurableTimerTests
    {
        private const DateTimeKind utc = DateTimeKind.Utc;
        private DateTime time = new DateTime(2020, 1, 1, 0, 0, 0, 0, utc);
        private int longIntervalSeconds = 5;
        private int shortIntervalSeconds = 3;
        private OrchestrationInvoker _orchestrationInvoker = new OrchestrationInvoker();
        private OrchestrationBindingInfo _orchestrationBindingInfo = new OrchestrationBindingInfo(
                                                                            "ContextParameterName",
                                                                            new OrchestrationContext());
        private Mock<IPowerShellServices> _mockPowerShellServices = new Mock<IPowerShellServices>();
        private DurableTimer _durableTimer = new DurableTimer();
        private int _nextEventId = 1;

        // Verifies that CreateTimer waits for the time elapsed 
        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void CreateTimerOrContinue_WaitsUntilTimerFires(bool timerScheduledAndFired, bool expectedWaitForStop)
        {
            int durationSeconds = longIntervalSeconds;
            DateTime startTime = time;
            DateTime fireAt = startTime.AddSeconds(durationSeconds);
            DateTime restartTime = fireAt.AddSeconds(shortIntervalSeconds);
            
            var history = MergeHistories(
                CreateOrchestratorStartedHistory(date: startTime, isProcessed: true),
                CreateTimerFiredHistory(timerScheduledAndFired: timerScheduledAndFired, fireAt: fireAt, restartTime: restartTime, orchestratorStartedIsProcessed: false)
            );
            var context = new OrchestrationContext { History = history, CurrentUtcDateTime = startTime };

            VerifyWaitForTimerFired(
                durableTimer: _durableTimer,
                durationSeconds: durationSeconds,
                expectedWaitForStop: expectedWaitForStop,
                () =>
                {
                    _durableTimer.CreateTimerAndStop_OrContinue(context: context, fireAt: fireAt);
                });
        }

        // Verifies that CreateTimerOrContinue updates CurrentUtcDateTime property to the next OrchestratorStarted event's Timestamp if the timer fired
        // If the timer has not fired, then CurrentUtcDateTime should not be updated
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CreateTimerOrContinue_UpdatesCurrentUtcDateTimeToNextOrchestratorStartedTimestamp_OnlyIfTimerCreatedAndFired(bool timerScheduledAndFired)
        {
            DateTime startTime = time;
            DateTime fireAt = startTime.AddSeconds(shortIntervalSeconds);
            DateTime restartTime = startTime.AddSeconds(longIntervalSeconds);
            DateTime shouldNotHitTime = restartTime.AddSeconds(longIntervalSeconds);
            var history = MergeHistories(
                CreateOrchestratorStartedHistory(date: startTime, isProcessed: true),
                CreateTimerFiredHistory(timerScheduledAndFired: timerScheduledAndFired, fireAt: fireAt, restartTime: restartTime, orchestratorStartedIsProcessed: false),
                CreateOrchestratorStartedHistory(date: shouldNotHitTime, isProcessed: false)
            );
            OrchestrationContext context = new OrchestrationContext { History = history, CurrentUtcDateTime = startTime };
            _orchestrationBindingInfo = new OrchestrationBindingInfo("ContextParameterName", context);
            if (!timerScheduledAndFired)
            {
                EmulateStop(_durableTimer);
            }

            _durableTimer.CreateTimerAndStop_OrContinue(context:context, fireAt: fireAt);

            if (timerScheduledAndFired)
            {
                Assert.Equal(restartTime, context.CurrentUtcDateTime);
            }
            else
            {
                Assert.Equal(startTime, context.CurrentUtcDateTime);
            }
        }

        private HistoryEvent[] CreateTimerFiredHistory(bool timerScheduledAndFired, DateTime fireAt, DateTime restartTime, bool orchestratorStartedIsProcessed)
        {
            var history = new List<HistoryEvent>();

            if (timerScheduledAndFired)
            {
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

        private static HistoryEvent[] MergeHistories(params HistoryEvent[][] histories)
        {
            return histories.Aggregate((a, b) => a.Concat(b).ToArray());
        }

        private int GetUniqueEventId()
        {
            return _nextEventId++;
        }

        private Hashtable InvokeOrchestration(bool completed, PSDataCollection<object> output = null)
        {
            var invocationAsyncResult = CreateInvocationResult(completed);
            ExpectBeginInvoke(invocationAsyncResult);
            if (!completed)
            {
                SignalToStopInvocation();
            }
            
            var result = _orchestrationInvoker.Invoke(_orchestrationBindingInfo, _mockPowerShellServices.Object);
            return result;
        }

        private IAsyncResult CreateInvocationResult(bool completed)
        {
            var completionEvent = new AutoResetEvent(initialState: completed);
            var result = new Mock<IAsyncResult>();
            result.Setup(_ => _.AsyncWaitHandle).Returns(completionEvent);
            return result.Object;
        }

        private void ExpectBeginInvoke(IAsyncResult invocationAsyncResult, PSDataCollection<object> output = null)
        {
            _mockPowerShellServices.Setup(_ => _.BeginInvoke(It.IsAny<PSDataCollection<object>>()))
                .Returns((PSDataCollection<object> outputBuffer) =>
                {
                    if (output != null)
                    {
                        foreach(var item in output)
                        {
                            outputBuffer.Add(item);
                        }
                    }

                    return invocationAsyncResult;
                });
        }

        private void EmulateStop(DurableTimer durableTimer)
        {
            durableTimer.Stop();
        }

        private void SignalToStopInvocation()
        {
            _orchestrationBindingInfo.Context.OrchestrationActionCollector.Stop();
        }

        private static void VerifyWaitForTimerFired(
            DurableTimer durableTimer,
            int durationSeconds,
            bool expectedWaitForStop,
            Action action
        )
        {
            var delayBeforeStopping = TimeSpan.FromSeconds(durationSeconds);

            // action() call may block until Stop is invoked from another thread.
            var thread = new Thread(() =>
            {
                Thread.Sleep(delayBeforeStopping);
                durableTimer.Stop();
            });
            thread.Start();
            try
            {
                var elapsedMilliseconds = MeasureExecutionTimeInMilliseconds(action);

                // Check if CreateTimerOrContinue was actually blocked
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
