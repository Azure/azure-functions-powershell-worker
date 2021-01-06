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
    using Microsoft.Azure.Functions.PowerShellWorker.Durable.Actions;

    internal class OrchestrationInvoker : IOrchestrationInvoker
    {
        public Hashtable Invoke(OrchestrationBindingInfo orchestrationBindingInfo, IPowerShellServices pwsh)
        {
            try
            {
                var outputBuffer = new PSDataCollection<object>();
                var context = orchestrationBindingInfo.Context;

                // context.History should never be null when initializing CurrentUtcDateTime
                var orchestrationStart = context.History.First(
                    e => e.EventType == HistoryEventType.OrchestratorStarted);
                context.CurrentUtcDateTime = orchestrationStart.Timestamp.ToUniversalTime();

                // Marks the first OrchestratorStarted event as processed
                orchestrationStart.IsProcessed = true;
                
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
                    try
                    {
                        // The orchestration function completed
                        pwsh.EndInvoke(asyncResult);
                        var result = FunctionReturnValueBuilder.CreateReturnValueFromFunctionOutput(outputBuffer);
                        return CreateOrchestrationResult(isDone: true, actions, output: result);
                    }
                    catch (Exception e)
                    {
                        // The orchestrator code has thrown an unhandled exception:
                        // this should be treated as an entire orchestration failure
                        throw new OrchestrationFailureException(actions, e);
                    }
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
