//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Utility;
    internal class DurableTaskHandler
    {
        private readonly ManualResetEvent _waitForStop = new ManualResetEvent(initialState: false);

        public void StopAndInitiateDurableTaskOrReplay(
            DurableTask task,
            OrchestrationContext context,
            bool noWait,
            Action<object> output)
        {
            context.OrchestrationActionCollector.Add(task.CreateOrchestrationAction());

            if (noWait)
            {
                output(task);
            }
            else
            {
                var taskScheduled = task.GetTaskScheduledHistoryEvent(context);
                var taskCompleted = task.GetTaskCompletedHistoryEvent(context, taskScheduled);

                if (taskCompleted != null)
                {                         
                    CurrentUtcDateTimeUpdater.UpdateCurrentUtcDateTime(context);

                    taskScheduled.IsProcessed = true;
                    taskCompleted.IsProcessed = true;
                    
                    if (GetEventResult(taskCompleted) != null)
                    {
                        output(GetEventResult(taskCompleted));
                    }
                }
                else
                {
                    InitiateAndWaitForStop(context);
                }
            }
        }

        // Waits for all of tasks to complete
        public void WaitAll(
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
                    if (GetEventResult(completedEvent) != null)
                    {
                        output(GetEventResult(completedEvent));
                    }
                }
            }
            else
            {
                InitiateAndWaitForStop(context);
            }
        }

        public void Stop()
        {
            _waitForStop.Set();
        }

        private static object GetEventResult(HistoryEvent historyEvent)
        {
            // Output the result if and only if the history event is a completed activity function
            return historyEvent.EventType != HistoryEventType.TaskCompleted
                ? null
                : TypeExtensions.ConvertFromJson(historyEvent.Result);
        }

        private void InitiateAndWaitForStop(OrchestrationContext context)
        {
            context.OrchestrationActionCollector.Stop();
            _waitForStop.WaitOne();
        }
    }
}
