//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // "Mixing XML comments for publicly visible type"

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Utility;
    public abstract class DurableTask
    {
        private static readonly ManualResetEvent _waitForStop = new ManualResetEvent(initialState: false);

        // Waits for all of tasks to complete
        internal static void WaitAll(
            IReadOnlyCollection<DurableTask> tasksToWaitFor,
            OrchestrationContext context,
            Action<object> output)
        {
            var completedEvents = new List<HistoryEvent>();
            foreach (var task in tasksToWaitFor)
            {
                var taskScheduled = task.GetTaskScheduledHistoryEvent(context);
                var taskCompleted = task.GetTaskCompletedHistoryEvent(context, taskScheduled);

                if (taskCompleted == null)
                {
                    break;
                }

                taskScheduled.IsProcessed = true;
                taskCompleted.IsProcessed = true;
                completedEvents.Add(taskCompleted);
            }

            var allTasksCompleted = completedEvents.Count == tasksToWaitFor.Count;
            if (allTasksCompleted)
            {
                CurrentUtcDateTimeUpdater.UpdateCurrentUtcDateTime(context);

                foreach (var completedEvent in completedEvents)
                {
                    output(GetEventResult(completedEvent));
                }
            }
            else
            {
                InitiateAndWaitForStop(context);
            }
        }

        internal abstract HistoryEvent GetTaskScheduledHistoryEvent(OrchestrationContext context);
        
        internal abstract HistoryEvent GetTaskCompletedHistoryEvent(OrchestrationContext context, HistoryEvent taskScheduled);

        private static object GetEventResult(HistoryEvent historyEvent)
        {
            // output the result if and only if the history event is a completed activity function
            return historyEvent.EventType == HistoryEventType.TaskCompleted
                ? null
                : TypeExtensions.ConvertFromJson(historyEvent.Result);
        }   

        private static void InitiateAndWaitForStop(OrchestrationContext context)
        {
            context.OrchestrationActionCollector.Stop();
            _waitForStop.WaitOne();
        }

        private static void Stop()
        {
            _waitForStop.Set();
        }

    }
}
