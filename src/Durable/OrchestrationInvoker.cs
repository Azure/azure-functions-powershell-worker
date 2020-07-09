//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;

    using PowerShellWorker.Utility;

    internal class OrchestrationInvoker : IOrchestrationInvoker
    {
        public Hashtable Invoke(OrchestrationBindingInfo orchestrationBindingInfo, IPowerShellServices pwsh)
        {
            try
            {
                var outputBuffer = new PSDataCollection<object>();
<<<<<<< HEAD
                var context = orchestrationBindingInfo.Context;

                // context.History should never be null when initializing CurrentUtcDateTime
                var orchestrationStart = context.History.First(
                    e => e.EventType == HistoryEventType.OrchestratorStarted);
                context.CurrentUtcDateTime = orchestrationStart.Timestamp.ToUniversalTime();

                // Marks the first OrchestratorStarted event as processed
                orchestrationStart.IsProcessed = true;
=======

                // Initialize CurrentUtcDateTime
                var context = orchestrationBindingInfo.Context;
                var orchestrationStart = context.History.FirstOrDefault(
                    (e) => e.EventType == HistoryEventType.OrchestratorStarted);

                // OrchestrationStart should never be null
                if (orchestrationStart == null)
                {
                    throw new ArgumentNullException(nameof(orchestrationStart));
                }
                else
                {
                    context.CurrentUtcDateTime = orchestrationStart.Timestamp.ToUniversalTime();
                }
>>>>>>> 9fd7379... Added CurrentUtcDateTime instance property to OrchestrationContext and CurrentUtcDateTime unit tests
                
                var asyncResult = pwsh.BeginInvoke(outputBuffer);

                var (shouldStop, actions) =
                    orchestrationBindingInfo.Context.OrchestrationActionCollector.WaitForActions(asyncResult.AsyncWaitHandle);

                if (shouldStop)
                {
                    // The orchestration function should be stopped and restarted
                    pwsh.StopInvoke();
                    return CreateOrchestrationResult(isDone: false, actions, output: null);
                }
                else
                {
                    // The orchestration function completed
                    pwsh.EndInvoke(asyncResult);
                    var result = FunctionReturnValueBuilder.CreateReturnValueFromFunctionOutput(outputBuffer);
                    return CreateOrchestrationResult(isDone: true, actions, output: result);
                }
            }
            finally
            {
                pwsh.ClearStreamsAndCommands();
            }
        }

        private static Hashtable CreateOrchestrationResult(
            bool isDone,
            List<OrchestrationAction> actions,
            object output)
        {
            var orchestrationMessage = new OrchestrationMessage(isDone, new List<List<OrchestrationAction>> { actions }, output);
            return new Hashtable { { AzFunctionInfo.DollarReturn, orchestrationMessage } };
        }
    }
}
