//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Linq;
    using System.Threading;
    using Utility;

    internal class DurableTimer
    {
        private readonly ManualResetEvent _waitForStop = new ManualResetEvent(initialState: false);

        public void CreateTimerOrContinue(OrchestrationContext context, DateTime fireAt, Action<object> output)
        {
            context.OrchestrationActionCollector.Add(new CreateDurableTimerAction(fireAt));

            var timerCreated = GetTimerCreated(context, fireAt);
            var timerFired = GetTimerFired(context, timerCreated);

            if (timerCreated == null)
            {
                CreateTimerAndWaitUntilFired(context);
            }

        }

        private static HistoryEvent GetTimerCreated(OrchestrationContext context, DateTime fireAt)
        {
            return context.History.FirstOrDefault(
                (e) => e.EventType == HistoryEventType.TimerCreated &&
                e.FireAt.Equals(fireAt)
                );
        }

        private static HistoryEvent GetTimerFired(OrchestrationContext context, HistoryEvent timerCreated)
        {
            if (timerCreated != null)
            {
                return context.History.FirstOrDefault(
                    (e) => e.EventType == HistoryEventType.TimerFired &&
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
