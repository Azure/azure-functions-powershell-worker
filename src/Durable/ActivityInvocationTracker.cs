//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using Utility;

    internal class ActivityInvocationTracker
    {
        private readonly ManualResetEvent _waitForStop = new ManualResetEvent(initialState: false);

        public void ReplayActivityOrStop(
            string functionName,
            object functionInput,
            OrchestrationContext context,
            bool noWait,
            Action<object> output)
        {
            context.OrchestrationActionCollector.Add(new CallActivityAction(functionName, functionInput));

            if (noWait)
            {
                output(new ActivityInvocationTask(functionName));
            }
            else
            {
                var taskScheduled = GetTaskScheduledHistoryEvent(context, functionName);
                var taskCompleted = GetTaskCompletedHistoryEvent(context, taskScheduled);

                if (taskCompleted != null)
                {
                    taskScheduled.IsProcessed = true;
                    taskCompleted.IsProcessed = true;
                    output(GetEventResult(taskCompleted));
                }
                else
                {
                    InitiateAndWaitForStop(context);
                }
            }
        }

        public void WaitForActivityTasks(
            IReadOnlyCollection<ActivityInvocationTask> tasksToWaitFor,
            OrchestrationContext context,
            Action<object> output)
        {
            var completedEvents = new List<HistoryEvent>();
            foreach (var task in tasksToWaitFor)
            {
                var taskScheduled = GetTaskScheduledHistoryEvent(context, task.Name);
                var taskCompleted = GetTaskCompletedHistoryEvent(context, taskScheduled);

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

        public void Stop()
        {
            _waitForStop.Set();
        }

        private void InitiateAndWaitForStop(OrchestrationContext context)
        {
            context.OrchestrationActionCollector.Stop();
            _waitForStop.WaitOne();
        }

        private static HistoryEvent GetTaskScheduledHistoryEvent(OrchestrationContext context, string functionName)
        {
            return context.History.FirstOrDefault(
                e => e.EventType == HistoryEventType.TaskScheduled &&
                     e.Name == functionName &&
                     !e.IsProcessed);
        }

        private static HistoryEvent GetTaskCompletedHistoryEvent(OrchestrationContext context, HistoryEvent taskScheduled)
        {
            return taskScheduled == null
                ? null
                : context.History.FirstOrDefault(
                    e => e.EventType == HistoryEventType.TaskCompleted &&
                         e.TaskScheduledId == taskScheduled.EventId);
        }

        private static object GetEventResult(HistoryEvent historyEvent)
        {
            return TypeExtensions.ConvertFromJson(historyEvent.Result);
        }
    }
}
