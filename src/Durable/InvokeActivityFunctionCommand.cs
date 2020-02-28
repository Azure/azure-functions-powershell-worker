//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // Missing XML comment for publicly visible type or member 'member'

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System.Collections;
    using System.Management.Automation;

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
        private readonly ActivityInvocationTracker _activityInvocationTracker = new ActivityInvocationTracker();

        protected override void EndProcessing()
        {
            var privateData = (Hashtable)MyInvocation.MyCommand.Module.PrivateData;
            var context = (OrchestrationContext)privateData[SetFunctionInvocationContextCommand.ContextKey];
            _activityInvocationTracker.ReplayActivityOrStop(FunctionName, Input, context, WriteObject);
        }

        protected override void StopProcessing()
        {
            _activityInvocationTracker.Stop();
        }
    }
}
