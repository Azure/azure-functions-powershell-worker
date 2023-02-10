//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable.Tasks;
    using Utility;

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
                                                output(TypeExtensions.ConvertFromJson(result));
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

        public static bool IsNonTerminalTaskFailedEvent(
            DurableTask task,
            OrchestrationContext context,
            HistoryEvent scheduledHistoryEvent,
            HistoryEvent completedHistoryEvent
            )
        {

            if (task is ActivityInvocationTask activity && completedHistoryEvent.EventType == HistoryEventType.TaskFailed)
            {
                if (activity.RetryOptions == null)
                {
                    return false;
                }
                else
                {
                    Action<string> NoOp = _ => { };
                    // RetryProcessor assumes events have not been processed,
                    // it will re-assign the `IsProcessed` flag for these events
                    // it its execution
                    scheduledHistoryEvent.IsProcessed = false;
                    completedHistoryEvent.IsProcessed = false;

                    var isFinalFailureEvent =
                        RetryProcessor.Process(
                            context.History,
                            scheduledHistoryEvent,
                            activity.RetryOptions.MaxNumberOfAttempts,
                            onSuccess: NoOp,
                            onFinalFailure: NoOp);
                    return !isFinalFailureEvent;
                }
            }
            return true;
        }

        // Waits for all of the given DurableTasks to complete
        public void WaitAll(
            IReadOnlyCollection<DurableTask> tasksToWaitFor,
            OrchestrationContext context,
            Action<object> output,
            Action<string> onFailure
            )
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

                if (IsNonTerminalTaskFailedEvent(task, context, scheduledHistoryEvent, completedHistoryEvent))
                {
                    // do not count this as a terminal event for this task
                    continue;
                }

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
                    if (completedHistoryEvent.EventType is HistoryEventType.TaskFailed)
                    {
                        onFailure(completedHistoryEvent.Reason);
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
            Action<object> output,
            Action<string> onFailure)
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
                    scheduledHistoryEvent.IsPlayed = true;
                }

                if (completedHistoryEvent == null)
                {
                    continue;
                }

                if (IsNonTerminalTaskFailedEvent(task, context, scheduledHistoryEvent, completedHistoryEvent))
                {
                    // do not count this as a terminal event for this task
                    completedHistoryEvent = null;
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
                    completedHistoryEvent.IsPlayed = true;
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

        public void GetTaskResult(
            IReadOnlyCollection<DurableTask> tasksToQueryResultFor,
            OrchestrationContext context,
            Action<object> output)
        {
            foreach (var task in tasksToQueryResultFor) {
                var scheduledHistoryEvent = task.GetScheduledHistoryEvent(context, true);
                var processedHistoryEvent = task.GetCompletedHistoryEvent(context, scheduledHistoryEvent, true);
                if (processedHistoryEvent != null)
                {
                    output(GetEventResult(processedHistoryEvent));
                }
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
