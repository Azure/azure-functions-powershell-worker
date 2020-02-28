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

    internal class ActivityInvocationTracker
    {
        private readonly ManualResetEvent _waitForStop = new ManualResetEvent(initialState: false);

        public void ReplayActivityOrStop(
            string functionName,
            object functionInput,
            OrchestrationContext context,
            Action<object> output)
        {
            context.OrchestrationActionCollector.Add(new CallActivityAction(functionName, functionInput));

            var taskScheduled = context.History.FirstOrDefault(
                e => e.EventType == HistoryEventType.TaskScheduled &&
                     e.Name == functionName &&
                     !e.IsProcessed);

            var taskCompleted = taskScheduled == null
                ? null
                : context.History.FirstOrDefault(
                    e => e.EventType == HistoryEventType.TaskCompleted &&
                         e.TaskScheduledId == taskScheduled.EventId);

            if (taskCompleted != null)
            {
                taskScheduled.IsProcessed = true;
                taskCompleted.IsProcessed = true;
                output(TypeExtensions.ConvertFromJson(taskCompleted.Result));
            }
            else
            {
                context.OrchestrationActionCollector.StopEvent.Set();
                _waitForStop.WaitOne();
            }
        }

        public void Stop()
        {
            _waitForStop.Set();
        }
    }
}
