//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Management.Automation;
    using System.Threading;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable.Tasks;
    using Microsoft.PowerShell.Commands;

    internal class DurableTaskHandler
    {
        private readonly ManualResetEvent _waitForStop = new ManualResetEvent(initialState: false);

        public void StopAndInitiateDurableTaskOrReplay(
            DurableTask task,
            OrchestrationContext context,
            bool noWait,
            Action<object> output,
            Action<string> onFailure,
            RetryOptions retryOptions = null)
        {
            context.OrchestrationActionCollector.Add(task.CreateOrchestrationAction());

            if (noWait)
            {
                output(task);
            }
            else
            {
                context.OrchestrationActionCollector.NextBatch();

                var scheduledHistoryEvent = task.GetScheduledHistoryEvent(context);
                var completedHistoryEvent = task.GetCompletedHistoryEvent(context, scheduledHistoryEvent);

                // We must check if the task has been completed first, otherwise external events will always wait upon replays
                if (completedHistoryEvent != null)
                {                         
                    CurrentUtcDateTimeUpdater.UpdateCurrentUtcDateTime(context);

                    if (scheduledHistoryEvent != null)
                    {
                        scheduledHistoryEvent.IsProcessed = true;
                    }

                    completedHistoryEvent.IsProcessed = true;
                    context.IsReplaying = completedHistoryEvent.IsPlayed;

                    switch (completedHistoryEvent.EventType)
                    {
                        case HistoryEventType.TaskCompleted:
                            var eventResult = GetEventResult(completedHistoryEvent);
                            if (eventResult != null)
                            {
                                output(eventResult);
                            }
                            break;
                        case HistoryEventType.EventRaised:
                            var eventRaisedResult = GetEventResult(completedHistoryEvent);
                            if (eventRaisedResult != null)
                            {
                                output(eventRaisedResult);
                            }
                            break;

                        case HistoryEventType.TaskFailed:
                            if (retryOptions == null)
                            {
                                onFailure(completedHistoryEvent.Reason);
                            }
                            else
                            {
                                // Reset IsProcessed, let RetryProcessor handle these events instead.
                                scheduledHistoryEvent.IsProcessed = false;
                                completedHistoryEvent.IsProcessed = false;

                                var shouldContinueProcessing =
                                    RetryProcessor.Process(
                                        context.History,
                                        scheduledHistoryEvent,
                                        retryOptions.MaxNumberOfAttempts,
                                        onSuccess:
                                            result => {
                                                output(ConvertFromJson(result));
                                            },
                                        onFailure);
                                        
                                if (shouldContinueProcessing)
                                {
                                    InitiateAndWaitForStop(context);
                                }
                            }
                            break;
                    }
                }
                else
                {
                    InitiateAndWaitForStop(context);
                }
            }
        }

        // Waits for all of the given DurableTasks to complete
        public void WaitAll(
            IReadOnlyCollection<DurableTask> tasksToWaitFor,
            OrchestrationContext context,
            Action<object> output)
        {
            context.OrchestrationActionCollector.NextBatch();
                
            var completedEvents = new List<HistoryEvent>();
            foreach (var task in tasksToWaitFor)
            {
                var scheduledHistoryEvent = task.GetScheduledHistoryEvent(context);
                var completedHistoryEvent = task.GetCompletedHistoryEvent(context, scheduledHistoryEvent);

                if (completedHistoryEvent == null)
                {
                    break;
                }

                if (scheduledHistoryEvent != null)
                {
                    scheduledHistoryEvent.IsProcessed = true;
                }

                completedHistoryEvent.IsProcessed = true;
                completedEvents.Add(completedHistoryEvent);
            }

            var allTasksCompleted = completedEvents.Count == tasksToWaitFor.Count;
            if (allTasksCompleted)
            {
                context.IsReplaying = completedEvents.Count == 0 ? false : completedEvents[0].IsPlayed;
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
            context.OrchestrationActionCollector.NextBatch();
                
            var completedTasks = new List<DurableTask>();
            DurableTask firstCompletedTask = null;
            int firstCompletedHistoryEventIndex = -1;

            foreach (var task in tasksToWaitFor)
            {
                var scheduledHistoryEvent = task.GetScheduledHistoryEvent(context);
                var completedHistoryEvent = task.GetCompletedHistoryEvent(context, scheduledHistoryEvent);

                // We must mark this event as processed even if it has not completed; subsequent completed history events
                // corresponding to an awaited task will not have their IsProcessed value ever set to true.
                if (scheduledHistoryEvent != null)
                {
                    scheduledHistoryEvent.IsProcessed = true;
                }

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

                    completedHistoryEvent.IsProcessed = true;
                }
            }

            var anyTaskCompleted = completedTasks.Count > 0;
            if (anyTaskCompleted)
            {
                context.IsReplaying = context.History[firstCompletedHistoryEventIndex].IsPlayed;
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
                return ConvertFromJson(historyEvent.Result);
            }
            else if (historyEvent.EventType == HistoryEventType.EventRaised)
            {
                return ConvertFromJson(historyEvent.Input);
            }
            return null;
        }

        public static object ConvertFromJson(string json)
        {
            object retObj = JsonObject.ConvertFromJson(json, returnHashtable: true, error: out _);

            if (retObj is PSObject psObj)
            {
                retObj = psObj.BaseObject;
            }

            if (retObj is Hashtable hashtable)
            {
                try
                {
                    // ConvertFromJson returns case-sensitive Hashtable by design -- JSON may contain keys that only differ in case.
                    // We try casting the Hashtable to a case-insensitive one, but if that fails, we keep using the original one.
                    retObj = new Hashtable(hashtable, StringComparer.OrdinalIgnoreCase);
                }
                catch
                {
                    retObj = hashtable;
                }
            }

            return retObj;
        }

        private void InitiateAndWaitForStop(OrchestrationContext context)
        {
            context.OrchestrationActionCollector.Stop();
            _waitForStop.WaitOne();
        }
    }
}
