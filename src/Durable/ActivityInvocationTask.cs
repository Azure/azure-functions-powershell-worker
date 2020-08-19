//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // "Missing XML comments for publicly visible type"

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Linq;
    using System.Collections.Generic;

    using WebJobs.Script.Grpc.Messages;

    public class ActivityInvocationTask : DurableTask
    {
        internal string FunctionName { get; }

        private object FunctionInput { get; }

        internal ActivityInvocationTask(string functionName, object functionInput)
        {
            FunctionName = functionName;
            FunctionInput = functionInput;
        }

        internal override HistoryEvent GetTaskScheduledHistoryEvent(OrchestrationContext context)
        {
            return context.History.FirstOrDefault(
                e => e.EventType == HistoryEventType.TaskScheduled &&
                     e.Name == FunctionName &&
                     !e.IsProcessed);
        }

        internal override HistoryEvent GetTaskCompletedHistoryEvent(OrchestrationContext context, HistoryEvent taskScheduled)
        {
            return taskScheduled == null
                ? null
                : context.History.FirstOrDefault(
                    e => e.EventType == HistoryEventType.TaskCompleted &&
                         e.TaskScheduledId == taskScheduled.EventId);
        }

        internal override OrchestrationAction CreateOrchestrationAction()
        {
            return new CallActivityAction(FunctionName, FunctionInput);
        }

        internal static void ValidateTask(ActivityInvocationTask task, IEnumerable<AzFunctionInfo> loadedFunctions)
        {
            var functionInfo = loadedFunctions.FirstOrDefault(fi => fi.FuncName == task.FunctionName);
            if (functionInfo == null)
            {
                var message = string.Format(PowerShellWorkerStrings.FunctionNotFound, task.FunctionName);
                throw new InvalidOperationException(message);
            }

            var activityTriggerBinding = functionInfo.InputBindings.FirstOrDefault(
                                            entry => DurableBindings.IsActivityTrigger(entry.Value.Type)
                                                     && entry.Value.Direction == BindingInfo.Types.Direction.In);
            if (activityTriggerBinding.Key == null)
            {
                var message = string.Format(PowerShellWorkerStrings.FunctionDoesNotHaveProperActivityFunctionBinding, task.FunctionName);
                throw new InvalidOperationException(message);
            }
        }
    }
}
