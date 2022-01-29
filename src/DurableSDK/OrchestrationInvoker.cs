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
                
                // IDEA:
                // This seems to be where the user-code is allowed to run.
                // When using the new SDK, we'll want the user-code to send an `asyncResult`
                // with a specific flag/signature that tells the worker to short-circuit
                // its regular DF logic, and to return the value its been provided without further processing.
                // All weed is to make the orchestrationBinding info viewable to the user-code.
                var asyncResult = pwsh.BeginInvoke(outputBuffer);

                var (shouldStop, actions) =
                    orchestrationBindingInfo.Context.OrchestrationActionCollector.WaitForActions(asyncResult.AsyncWaitHandle);

                if (shouldStop)
                {
                    // The orchestration function should be stopped and restarted
                    pwsh.StopInvoke();
                    return CreateOrchestrationResult(isDone: false, actions, output: null, context.CustomStatus);
                }
                else
                {
                    try
                    {
                        // The orchestration function completed
                        pwsh.EndInvoke(asyncResult);
                        var result = FunctionReturnValueBuilder.CreateReturnValueFromFunctionOutput(outputBuffer);
                        return CreateOrchestrationResult(isDone: true, actions, output: result, context.CustomStatus);
                    }
                    catch (Exception e)
                    {
                        // The orchestrator code has thrown an unhandled exception:
                        // this should be treated as an entire orchestration failure
                        throw new OrchestrationFailureException(actions, context.CustomStatus, e);
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
            List<List<OrchestrationAction>> actions,
            object output,
            object customStatus)
        {
            var orchestrationMessage = new OrchestrationMessage(isDone, actions, output, customStatus);
            return new Hashtable { { AzFunctionInfo.DollarReturn, orchestrationMessage } };
        }
    }
}
