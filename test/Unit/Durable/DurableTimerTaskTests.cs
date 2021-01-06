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
    using Microsoft.Azure.Functions.PowerShellWorker.Durable.Actions;
    using Xunit;

    public class DurableTimerTaskTests
    {
        private static TimeSpan _longInterval = new TimeSpan(0, 0, 5);
        private static TimeSpan _shortInterval = new TimeSpan(0, 0, 3);
        private static readonly DateTime _startTime = DateTime.UtcNow;
        private static readonly DateTime _restartTime = _startTime.Add(_longInterval);
        private static readonly DateTime _shouldNotHitTime = _restartTime.Add(_longInterval);
        private readonly DateTime _fireAt = _startTime.Add(_shortInterval);
        private int _nextEventId = 1;

        // Verifies that StopAndInitiateDurableTaskOrReplay waits for the time elapsed 
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

            var durableTaskHandler = new DurableTaskHandler();

            DurableTestUtilities.VerifyWaitForDurableTasks(
                durableTaskHandler,
                delayBeforeStopping: _longInterval,
                expectedWaitForStop: expectedWaitForStop,
                () =>
                {
                    durableTaskHandler.StopAndInitiateDurableTaskOrReplay(
                        task: new DurableTimerTask(_fireAt), context: context, noWait: false,
                        output: _ => { Assert.True(false, "Unexpected output"); },
                        onFailure: _ => { });
                });
        }

        // Verifies that StopAndInitiateDurableTaskOrReplay updates CurrentUtcDateTime property to the next OrchestratorStarted event's Timestamp if the timer fired
        // If the timer has not fired, then CurrentUtcDateTime should not be updated
        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void StopAndInitiateDurableTaskOrReplay_UpdatesCurrentUtcDateTimeToNextOrchestratorStartedTimestamp_OnlyIfTimerCreatedAndFired(bool timerCreated, bool timerFired)
        {
            var history = DurableTestUtilities.MergeHistories(
                CreateOrchestratorStartedHistory(date: _startTime, isProcessed: true),
                CreateDurableTimerHistory(timerCreated: timerCreated, timerFired: timerFired, fireAt: _fireAt, restartTime: _restartTime, orchestratorStartedIsProcessed: false),
                CreateOrchestratorStartedHistory(date: _shouldNotHitTime, isProcessed: false)
            );
            var context = new OrchestrationContext { History = history, CurrentUtcDateTime = _startTime };

            var durableTaskHandler = new DurableTaskHandler();
            var task = new DurableTimerTask(_fireAt);

            if (!timerCreated || !timerFired)
            {
                DurableTestUtilities.EmulateStop(durableTaskHandler);
                durableTaskHandler.StopAndInitiateDurableTaskOrReplay(
                    task: task, context: context, noWait: false,
                    output: _ => { Assert.True(false, "Unexpected output"); },
                    onFailure: _ => { });
                Assert.Equal(_startTime, context.CurrentUtcDateTime);
            }
            else
            {
                durableTaskHandler.StopAndInitiateDurableTaskOrReplay(task: task, context: context, noWait: false, _ => { Assert.True(false, "Unexpected output"); }, errorMessage => { });
                Assert.Equal(_restartTime, context.CurrentUtcDateTime);
            }
            VerifyCreateDurableTimerActionAdded(context, _fireAt);
        }

        [Fact]
        public void StopAndInitiateDurableTaskOrReplay_ContinuesToNextTimer_IfTimersHaveIdenticalFireAt()
        {
            var history = DurableTestUtilities.MergeHistories(
                CreateOrchestratorStartedHistory(date: _startTime, isProcessed: true),
                CreateDurableTimerHistory(timerCreated: true, timerFired: true, fireAt: _fireAt, restartTime: _restartTime, orchestratorStartedIsProcessed: false),
                CreateDurableTimerHistory(timerCreated: true, timerFired: true, fireAt: _fireAt, restartTime: _restartTime, orchestratorStartedIsProcessed: false),
                CreateDurableTimerHistory(timerCreated: true, timerFired: true, fireAt: _fireAt, restartTime: _shouldNotHitTime, orchestratorStartedIsProcessed: false)
            );
            var context = new OrchestrationContext { History = history };

            var durableTaskHandler = new DurableTaskHandler();

            for (int i = 0; i < 2; i++) {
                durableTaskHandler.StopAndInitiateDurableTaskOrReplay(
                    task: new DurableTimerTask(_fireAt), context: context, noWait: false,
                    output: _ => { Assert.True(false, "Unexpected output"); },
                    onFailure: _ => { });
                Assert.Equal(_restartTime, context.CurrentUtcDateTime);
            }

            durableTaskHandler.StopAndInitiateDurableTaskOrReplay(
                task: new DurableTimerTask(_fireAt), context: context, noWait: false,
                output: _ => { Assert.True(false, "Unexpected output"); },
                onFailure: _ => { });
            Assert.Equal(_shouldNotHitTime, context.CurrentUtcDateTime);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void StopAndInitiateDurableTaskOrReplay_OutputsNothing_IfNoWaitNotRequested(bool timerCreated, bool timerFired)
        {
            var history = DurableTestUtilities.MergeHistories(
                CreateOrchestratorStartedHistory(date: _startTime, isProcessed: true),
                CreateDurableTimerHistory(timerCreated: timerCreated, timerFired: timerFired, fireAt: _fireAt, restartTime: _restartTime, orchestratorStartedIsProcessed: false)
            );
            var context = new OrchestrationContext { History = history };
            
            var durableTaskHandler = new DurableTaskHandler();

            if (!timerCreated || !timerFired)
            {
                DurableTestUtilities.EmulateStop(durableTaskHandler);
            }
            
            durableTaskHandler.StopAndInitiateDurableTaskOrReplay(
                task: new DurableTimerTask(_fireAt), context: context, noWait: false,
                output: _ => { Assert.True(false, "Unexpected output"); }, onFailure: _ => { });
            VerifyCreateDurableTimerActionAdded(context, _fireAt);
        }

        [Fact]
        public void StopAndInitiateDurableTaskOrReplay_OutputsDurableTimerTask_IfNoWaitRequested()
        {
            var history = CreateOrchestratorStartedHistory(date: _startTime, isProcessed: true);
            var context = new OrchestrationContext { History = history };
            var allOutput = new List<DurableTimerTask>();

            var durableTaskHandler = new DurableTaskHandler();

            durableTaskHandler.StopAndInitiateDurableTaskOrReplay(
                task: new DurableTimerTask(_fireAt), context: context, noWait: true,
                output: output => { allOutput.Add((DurableTimerTask)output); },
                onFailure: _ => { });
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

        private void VerifyCreateDurableTimerActionAdded(OrchestrationContext context, DateTime fireAt)
        {
            var actions = DurableTestUtilities.GetCollectedActions(context);
            var action = (CreateDurableTimerAction)actions.Last();
            Assert.Equal(action.FireAt, fireAt);
        }
    }
}
