//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;

using Newtonsoft.Json;
using Microsoft.Azure.Functions.PowerShellWorker.Action;
using Microsoft.Azure.Functions.PowerShellWorker.History;
using Microsoft.Azure.Functions.PowerShellWorker.Utility;

namespace Microsoft.Azure.Functions.PowerShellWorker.Commands
{
    /// <summary>
    /// Invoke a function asynchronously.
    /// </summary>
    [Cmdlet("Invoke", "ActivityFunctionAsync")]
    public class InvokeActivityFunctionCommand : PSCmdlet
    {
        /// <summary>
        /// Gets and sets the activity function name.
        /// </summary>
        [Parameter(Mandatory = true)]
        public string FunctionName { get; set; }

        /// <summary>
        /// Gets and sets the input for an activity function.
        /// </summary>
        /// <remarks>
        /// Copy the default value from durable-js, in case that it's a magic value for specifying no input value.
        /// </remarks>
        [Parameter]
        [ValidateNotNull]
        public object Input { get; set; } = "__activity__default";

        // Used for waiting on the pipeline to be stopped.
        private ManualResetEvent waitForStop = new ManualResetEvent(initialState: false);
        private OrchestrationContext context;

        /// <summary>
        /// Implement the EndProcessing method.
        /// </summary>
        protected override void EndProcessing()
        {
            var privateData = (Hashtable)this.MyInvocation.MyCommand.Module.PrivateData;
            context = (OrchestrationContext)privateData[SetFunctionInvocationContextCommand.ContextKey];

            context.Actions.Add(new List<AzAction>() { new CallActivityAction(FunctionName, Input) });

            HistoryEvent taskScheduled = context.History
                .FirstOrDefault(e => e.EventType == EventType.TaskScheduled &&
                                e.Name == FunctionName &&
                                !e.IsProcessed);

            HistoryEvent taskCompleted = taskScheduled == null ? null : context.History
                .FirstOrDefault(e => e.EventType == EventType.TaskCompleted &&
                                e.TaskScheduledId == taskScheduled.EventId);

            if (taskCompleted != null)
            {
                taskScheduled.IsProcessed = true;
                taskCompleted.IsProcessed = true;
                WriteObject(TypeExtensions.ConvertFromJson(taskCompleted.Result));
            }
            else
            {
                context.ActionEvent.Set();
                waitForStop.WaitOne();
            }
        }

        /// <summary>
        /// Implement the StopProcessing method.
        /// </summary>
        protected override void StopProcessing()
        {
            waitForStop.Set();
        }
    }
}
