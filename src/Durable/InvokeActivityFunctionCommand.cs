//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // Missing XML comment for publicly visible type or member 'member'

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using System.Threading;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    /// <summary>
    /// Invoke an activity function.
    /// </summary>
    [Cmdlet("Invoke", "ActivityFunction")]
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
        [Parameter]
        [ValidateNotNull]
        public object Input { get; set; }

        // Used for waiting on the pipeline to be stopped.
        private readonly ManualResetEvent _waitForStop = new ManualResetEvent(initialState: false);

        private OrchestrationContext _context;

        protected override void EndProcessing()
        {
            var privateData = (Hashtable)MyInvocation.MyCommand.Module.PrivateData;
            _context = (OrchestrationContext)privateData[SetFunctionInvocationContextCommand.ContextKey];

            _context.Actions.Add(new List<OrchestrationAction> { new CallActivityAction(FunctionName, Input) });

            var taskScheduled = _context.History.FirstOrDefault(
                                    e => e.EventType == HistoryEventType.TaskScheduled &&
                                         e.Name == FunctionName &&
                                         !e.IsProcessed);

            var taskCompleted = taskScheduled == null
                                    ? null
                                    : _context.History.FirstOrDefault(
                                        e => e.EventType == HistoryEventType.TaskCompleted &&
                                             e.TaskScheduledId == taskScheduled.EventId);

            if (taskCompleted != null)
            {
                taskScheduled.IsProcessed = true;
                taskCompleted.IsProcessed = true;
                WriteObject(TypeExtensions.ConvertFromJson(taskCompleted.Result));
            }
            else
            {
                _context.ActionEvent.Set();
                _waitForStop.WaitOne();
            }
        }

        protected override void StopProcessing()
        {
            _waitForStop.Set();
        }
    }
}
