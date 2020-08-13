//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Test.Durable
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable;
    using Xunit;

    public class DurableTimerTests
    {
        private static TimeSpan _longInterval = new TimeSpan(0, 0, 5);
        private static TimeSpan _shortInterval = new TimeSpan(0, 0, 3);
        private static readonly DateTime _startTime = DateTime.UtcNow;
        private static readonly DateTime _restartTime = _startTime.Add(_longInterval);
        private static readonly DateTime _shouldNotHitTime = _restartTime.Add(_longInterval);
        private readonly DateTime _fireAt = _startTime.Add(_shortInterval);

        private DurableTimer _durableTimer = new DurableTimer();
        private int _nextEventId = 1;

        // Verifies that CreateTimer waits for the time elapsed 
        [Theory]
        [InlineData(true, true, false)]
        [InlineData(false, false, true)]
        public void StopAndCreateTimerOrContinue_WaitsUntilTimerFires_IfNoWaitNotRequested(bool timerCreated, bool timerFired, bool expectedWaitForStop)
        {
            var history = DurableTestUtilities.MergeHistories(
                CreateOrchestratorStartedHistory(date: _startTime, isProcessed: true),
                CreateDurableTimerHistory(timerCreated: timerCreated, timerFired: timerFired, fireAt: _fireAt, restartTime: _restartTime, orchestratorStartedIsProcessed: false)
            );
            var context = new OrchestrationContext { History = history, CurrentUtcDateTime = _startTime };

            VerifyWaitForTimerFired(
                durableTimer: _durableTimer,
                durationSeconds: _longInterval,
                expectedWaitForStop: expectedWaitForStop,
                () =>
                {
                    _durableTimer.StopAndCreateTimerOrContinue(context: context, fireAt: _fireAt, noWait: false, _ => { });
                });
        }

        // Verifies that CreateTimerOrContinue updates CurrentUtcDateTime property to the next OrchestratorStarted event's Timestamp if the timer fired
        // If the timer has not fired, then CurrentUtcDateTime should not be updated
        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void StopAndCreateTimerOrContinue_UpdatesCurrentUtcDateTimeToNextOrchestratorStartedTimestamp_OnlyIfTimerCreatedAndFired(bool timerCreated, bool timerFired)
        {
            var history = DurableTestUtilities.MergeHistories(
                CreateOrchestratorStartedHistory(date: _startTime, isProcessed: true),
                CreateDurableTimerHistory(timerCreated: timerCreated, timerFired: timerFired, fireAt: _fireAt, restartTime: _restartTime, orchestratorStartedIsProcessed: false),
                CreateOrchestratorStartedHistory(date: _shouldNotHitTime, isProcessed: false)
            );
            OrchestrationContext context = new OrchestrationContext { History = history, CurrentUtcDateTime = _startTime };

            if (!timerCreated || !timerFired)
            {
                EmulateStop(_durableTimer);
                _durableTimer.StopAndCreateTimerOrContinue(context:context, fireAt: _fireAt, noWait: false, _ => { });
                Assert.Equal(_startTime, context.CurrentUtcDateTime);
            }
            else
            {
                _durableTimer.StopAndCreateTimerOrContinue(context:context, fireAt: _fireAt, noWait: false, _ => { });
                Assert.Equal(_restartTime, context.CurrentUtcDateTime);
            }
            VerifyCreateDurableTimerActionAdded(context, _fireAt);
        }

        [Fact]
        public void StopAndCreateTimerOrContinue_ContinuesToNextTimer_IfTimersHaveIdenticalFireAt()
        {
            var history = DurableTestUtilities.MergeHistories(
                CreateOrchestratorStartedHistory(date: _startTime, isProcessed: true),
                CreateDurableTimerHistory(timerCreated: true, timerFired: true, fireAt: _fireAt, restartTime: _restartTime, orchestratorStartedIsProcessed: false),
                CreateDurableTimerHistory(timerCreated: true, timerFired: true, fireAt: _fireAt, restartTime: _restartTime, orchestratorStartedIsProcessed: false),
                CreateDurableTimerHistory(timerCreated: true, timerFired: true, fireAt: _fireAt, restartTime: _shouldNotHitTime, orchestratorStartedIsProcessed: false)
            );

            var context = new OrchestrationContext { History = history };

            for (int i = 0; i < 2; i++) {
                _durableTimer.StopAndCreateTimerOrContinue(context: context, fireAt: _fireAt, noWait: false, _ => { });
                Assert.Equal(_restartTime, context.CurrentUtcDateTime);
            }

            _durableTimer.StopAndCreateTimerOrContinue(context: context, fireAt: _fireAt, noWait: false, _ => { });
            Assert.Equal(_shouldNotHitTime, context.CurrentUtcDateTime);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void StopAndCreateTimerOrContinue_OutputsNothing_IfNoWaitNotRequested(bool timerCreated, bool timerFired)
        {
            var history = DurableTestUtilities.MergeHistories(
                CreateOrchestratorStartedHistory(date: _startTime, isProcessed: true),
                CreateDurableTimerHistory(timerCreated: timerCreated, timerFired: timerFired, fireAt: _fireAt, restartTime: _restartTime, orchestratorStartedIsProcessed: false)
            );

            var context = new OrchestrationContext { History = history };

            if (!timerCreated)
            {
                EmulateStop(_durableTimer);
            }

            _durableTimer.StopAndCreateTimerOrContinue(context: context, fireAt: _fireAt, noWait: false, _ => { Assert.True(false, "Unexpected output"); } );
            VerifyCreateDurableTimerActionAdded(context, _fireAt);
        }

        [Fact]
        public void StopAndCreateTimerOrContinue_OutputsDurableTimerTask_IfNoWaitRequested()
        {
            var history = CreateOrchestratorStartedHistory(date: _startTime, isProcessed: true);
            var context = new OrchestrationContext { History = history };
            var allOutput = new List<DurableTimerTask>();

            _durableTimer.StopAndCreateTimerOrContinue(context: context, fireAt: _fireAt, noWait: true, output => { allOutput.Add((DurableTimerTask)output); });
            Assert.Equal(_fireAt, allOutput.Single().FireAt);
            VerifyCreateDurableTimerActionAdded(context, _fireAt);
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

                if (timerFired)
                {
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

        private void EmulateStop(DurableTimer durableTimer)
        {
            durableTimer.Stop();
        }

        private void VerifyCreateDurableTimerActionAdded(OrchestrationContext context, DateTime fireAt)
        {
            var actions = DurableTestUtilities.GetCollectedActions(context);
            var action = (CreateDurableTimerAction)actions.Last();
            Assert.Equal(action.FireAt, fireAt);
        }

        private void VerifyWaitForTimerFired(
            DurableTimer durableTimer,
            TimeSpan durationSeconds,
            bool expectedWaitForStop,
            Action action
        )
        {
            var delayBeforeStopping = durationSeconds;

            // action() call may block until Stop is invoked from another thread.
            var thread = new Thread(() =>
            {
                Thread.Sleep(delayBeforeStopping);
                durableTimer.Stop();
            });
            thread.Start();
            try
            {
                var elapsedMilliseconds = DurableTestUtilities.MeasureExecutionTimeInMilliseconds(action);

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
    }
}
