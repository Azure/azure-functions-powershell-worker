//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Linq;
    using System.Threading;

    internal class DurableTimer
    {
        private readonly ManualResetEvent _waitForStop = new ManualResetEvent(initialState: false);

        public void CreateTimerAndStop_OrContinue(OrchestrationContext context, DateTime fireAt)
        {
            context.OrchestrationActionCollector.Add(new CreateDurableTimerAction(fireAt));

            var timerCreated = GetTimerCreated(context, fireAt);
            var timerFired = GetTimerFired(context, timerCreated);

            if (timerCreated == null)
            {
                CreateTimerAndWaitUntilFired(context);
            }
            else if (timerFired != null)
            {
                // Update CurrentUtcDateTime if the timer has fired
                var newOrchestrationStart = context.History.FirstOrDefault(
                        e => e.EventType == HistoryEventType.OrchestratorStarted &&
                            !e.IsProcessed
                    );
                
                if (newOrchestrationStart != null)
                {
                    context.CurrentUtcDateTime = newOrchestrationStart.Timestamp.ToUniversalTime();
                    newOrchestrationStart.IsProcessed = true;
                }

                timerCreated.IsProcessed = true;
                timerFired.IsProcessed = true;
            }
        }

        private static HistoryEvent GetTimerCreated(OrchestrationContext context, DateTime fireAt)
        {
            return context.History.FirstOrDefault(
                e => e.EventType == HistoryEventType.TimerCreated &&
                     e.FireAt.Equals(fireAt) &&
                    !e.IsProcessed
                );
        }

        private static HistoryEvent GetTimerFired(OrchestrationContext context, HistoryEvent timerCreated)
        {
            if (timerCreated != null)
            {
                return context.History.FirstOrDefault(
                    e => e.EventType == HistoryEventType.TimerFired &&
                         e.TimerId == timerCreated.EventId
                    );
            }
            return null;
        }

        private void CreateTimerAndWaitUntilFired(OrchestrationContext context)
        {
            context.OrchestrationActionCollector.Stop();
            _waitForStop.WaitOne();
        }

        public void Stop()
        {
            _waitForStop.Set();
        }

    }
}
