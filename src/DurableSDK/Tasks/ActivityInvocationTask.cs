//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // "Missing XML comments for publicly visible type"

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable.Tasks
{
    using System.Linq;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable.Actions;
    using Microsoft.Azure.Functions.PowerShellWorker.DurableWorker;

    public class ActivityInvocationTask : DurableTask
    {
        internal string FunctionName { get; }

        private object Input { get; }

        private RetryOptions RetryOptions { get; }

        internal ActivityInvocationTask(string functionName, object functionInput, RetryOptions retryOptions)
        {
            FunctionName = functionName;
            Input = functionInput;
            RetryOptions = retryOptions;
        }

        internal ActivityInvocationTask(string functionName, object functionInput)
            : this(functionName, functionInput, retryOptions: null)
        {
        }

        internal override HistoryEvent GetScheduledHistoryEvent(OrchestrationContext context, bool processed)
        {
            return context.History.FirstOrDefault(
                e => e.EventType == HistoryEventType.TaskScheduled &&
                     e.Name == FunctionName &&
                     e.IsProcessed == processed);
        }

        internal override HistoryEvent GetCompletedHistoryEvent(OrchestrationContext context, HistoryEvent scheduledHistoryEvent, bool processed)
        {
            return scheduledHistoryEvent == null
                ? null
                : context.History.FirstOrDefault(
                    e => e.TaskScheduledId == scheduledHistoryEvent.EventId
                         && (e.EventType == HistoryEventType.TaskCompleted
                             || e.EventType == HistoryEventType.TaskFailed));
        }

        internal override OrchestrationAction CreateOrchestrationAction()
        {
            return RetryOptions == null
                ? new CallActivityAction(FunctionName, Input)
                : new CallActivityWithRetryAction(FunctionName, Input, RetryOptions);
        }
    }
}
