//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#pragma warning disable 1591 // Missing XML comment for publicly visible type or member 'member'

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable.Commands
{
    using System.Collections;
    using System.Management.Automation;
    using Microsoft.Azure.Functions.PowerShellWorker.Durable.Tasks;

    /// <summary>
    /// Invoke a durable activity.
    /// </summary>
    [Cmdlet("Invoke", "DurableActivity")]
    public class InvokeDurableActivityCommand : PSCmdlet
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

        [Parameter]
        public SwitchParameter NoWait { get; set; }

        [Parameter]
        [ValidateNotNull]
        public RetryOptions RetryOptions { get; set; }

        private readonly DurableTaskHandler _durableTaskHandler = new DurableTaskHandler();

        protected override void EndProcessing()
        {
            var privateData = (Hashtable)MyInvocation.MyCommand.Module.PrivateData;
            var context = (OrchestrationContext)privateData[SetFunctionInvocationContextCommand.ContextKey];
            var loadedFunctions = FunctionLoader.GetLoadedFunctions();

            var task = new ActivityInvocationTask(FunctionName, Input, RetryOptions);
            ActivityInvocationTask.ValidateTask(task, loadedFunctions);

            _durableTaskHandler.StopAndInitiateDurableTaskOrReplay(
                task, context, NoWait.IsPresent,
                output: WriteObject,
                onFailure: failureReason => DurableActivityErrorHandler.Handle(this, failureReason),
                retryOptions: RetryOptions);
        }

        protected override void StopProcessing()
        {
            _durableTaskHandler.Stop();
        }
    }
}
