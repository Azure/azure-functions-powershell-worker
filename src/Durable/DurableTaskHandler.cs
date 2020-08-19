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
                var scheduledHistoryEvent = task.GetScheduledHistoryEvent(context);
                var completedHistoryEvent = task.GetCompletedHistoryEvent(context, scheduledHistoryEvent);

                // Assume that the task scheduled must have completed if NoWait is not present and the orchestrator restarted
                if (scheduledHistoryEvent == null)
                {
                    InitiateAndWaitForStop(context);
                }

                if (completedHistoryEvent != null)
                {                         
                    CurrentUtcDateTimeUpdater.UpdateCurrentUtcDateTime(context);

                    scheduledHistoryEvent.IsProcessed = true;
                    completedHistoryEvent.IsProcessed = true;
                    
                    if (GetEventResult(completedHistoryEvent) != null)
                    {
                        output(GetEventResult(completedHistoryEvent));
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
                var scheduledHistoryEvent = task.GetScheduledHistoryEvent(context);
                var completedHistoryEvent = task.GetCompletedHistoryEvent(context, scheduledHistoryEvent);

                if (completedHistoryEvent == null)
                {
                    break;
                }

                scheduledHistoryEvent.IsProcessed = true;
                completedHistoryEvent.IsProcessed = true;
                completedEvents.Add(completedHistoryEvent);
            }

            var allTasksCompleted = completedEvents.Count == tasksToWaitFor.Count;
            if (allTasksCompleted)
            {
                CurrentUtcDateTimeUpdater.UpdateCurrentUtcDateTime(context);

                foreach (var completedHistoryEvent in completedEvents)
                {
                    if (GetEventResult(completedHistoryEvent) != null)
                    {
                        output(GetEventResult(completedHistoryEvent));
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
            DurableTask firstCompletedTask = null;
            int firstCompletedHistoryEventIndex = -1;

            foreach (var task in tasksToWaitFor)
            {
                var scheduledHistoryEvent = task.GetScheduledHistoryEvent(context);
                var completedHistoryEvent = task.GetCompletedHistoryEvent(context, scheduledHistoryEvent);

                
                if (completedHistoryEvent != null)
                {
                    completedTasks.Add(task);
                    int completedHistoryEventIndex = Array.IndexOf(context.History, completedHistoryEvent);

                    if (firstCompletedHistoryEventIndex < 0 ||
                        completedHistoryEventIndex < firstCompletedHistoryEventIndex)
                    {
                        firstCompletedHistoryEventIndex = completedHistoryEventIndex;
                        firstCompletedTask = task;
                    }

                    scheduledHistoryEvent.IsProcessed = true;
                    completedHistoryEvent.IsProcessed = true;
                }
            }

            var anyTaskCompleted = completedTasks.Count > 0;
            if (anyTaskCompleted)
            {
                CurrentUtcDateTimeUpdater.UpdateCurrentUtcDateTime(context);
                // Return a reference to the first completed task
                output(firstCompletedTask);
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
            // Output the result if and only if the history event is a completed activity function or a raised external event
            
            if (historyEvent.EventType == HistoryEventType.TaskCompleted)
            {
                return TypeExtensions.ConvertFromJson(historyEvent.Result);
            }
            else if (historyEvent.EventType == HistoryEventType.EventRaised)
            {
                return TypeExtensions.ConvertFromJson(historyEvent.Input);
            }
            return null;
        }

        private void InitiateAndWaitForStop(OrchestrationContext context)
        {
            context.OrchestrationActionCollector.Stop();
            _waitForStop.WaitOne();
        }
    }
}
