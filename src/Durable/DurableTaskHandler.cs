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

                // Assume that the task scheduled must have completed if NoWait is not present and the orchestrator restarted
                if (taskScheduled == null)
                {
                    InitiateAndWaitForStop(context);
                }

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
            }
        }

        // Waits for all of the given DurableTasks to complete
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

        // Waits for any one of the given DurableTasks to complete and outputs the first DurableTask that does so
        public void WaitAny(
            IReadOnlyCollection<DurableTask> tasksToWaitFor,
            OrchestrationContext context,
            Action<object> output)
        {
            var completedTasks = new List<DurableTask>();
            int earliestCompletedHistoryEventIndex = context.History.Length;
            int earliestCompletedTaskIndex = -1;

            foreach (var task in tasksToWaitFor)
            {
                var taskScheduled = task.GetTaskScheduledHistoryEvent(context);
                var taskCompleted = task.GetTaskCompletedHistoryEvent(context, taskScheduled);

                taskScheduled.IsProcessed = true;

                if (taskCompleted != null)
                {
                    completedTasks.Add(task);
                    int completedHistoryEventIndex = Array.IndexOf(context.History, taskCompleted);

                    if (completedHistoryEventIndex < earliestCompletedHistoryEventIndex)
                    {
                        earliestCompletedHistoryEventIndex = completedHistoryEventIndex;
                        earliestCompletedTaskIndex = completedTasks.LastIndexOf(task);
                    }

                    taskCompleted.IsProcessed = true;
                }
            }

            var anyTaskCompleted = completedTasks.Count > 0;
            if (anyTaskCompleted)
            {
                CurrentUtcDateTimeUpdater.UpdateCurrentUtcDateTime(context);
                output(completedTasks[earliestCompletedTaskIndex]);
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
