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

    using Microsoft.Azure.Functions.PowerShellWorker.Durable.Actions;
    using Microsoft.Azure.Functions.PowerShellWorker.Utility;

    internal class OrchestrationInvoker : IOrchestrationInvoker
    {
        private IExternalOrchestrationInvoker externalInvoker;

        public Hashtable Invoke(
            OrchestrationBindingInfo orchestrationBindingInfo,
            IPowerShellServices powerShellServices)
        {
            try
            {
                if (powerShellServices.HasExternalDurableSDK())
                {
                    return InvokeExternalDurableSDK(powerShellServices);
                }
                return InvokeInternalDurableSDK(orchestrationBindingInfo, powerShellServices);
            }
            catch (Exception ex)
            {
                ex.Data.Add(Utils.IsOrchestrationFailureKey, true);
                throw;
            }
            finally
            {
                powerShellServices.ClearStreamsAndCommands();
            }
        }

        public Hashtable InvokeExternalDurableSDK(IPowerShellServices powerShellServices)
        {
            return externalInvoker.Invoke(powerShellServices);
        }

        public Hashtable InvokeInternalDurableSDK(
            OrchestrationBindingInfo orchestrationBindingInfo,
            IPowerShellServices powerShellServices)
        {
            var outputBuffer = new PSDataCollection<object>();
            var context = orchestrationBindingInfo.Context;

            // context.History should never be null when initializing CurrentUtcDateTime
            var orchestrationStart = context.History.First(
                e => e.EventType == HistoryEventType.OrchestratorStarted);
            context.CurrentUtcDateTime = orchestrationStart.Timestamp.ToUniversalTime();

            // Marks the first OrchestratorStarted event as processed
            orchestrationStart.IsProcessed = true;

            // Finish initializing the Function invocation
            powerShellServices.AddParameter(orchestrationBindingInfo.ParameterName, context);
            powerShellServices.TracePipelineObject();

            var asyncResult = powerShellServices.BeginInvoke(outputBuffer);

            var (shouldStop, actions) =
                orchestrationBindingInfo.Context.OrchestrationActionCollector.WaitForActions(asyncResult.AsyncWaitHandle);

            if (shouldStop)
            {
                // The orchestration function should be stopped and restarted
                powerShellServices.StopInvoke();
                return CreateOrchestrationResult(isDone: false, actions, output: null, context.CustomStatus);
            }
            else
            {
                try
                {
                    // The orchestration function completed
                    powerShellServices.EndInvoke(asyncResult);
                    var result = CreateReturnValueFromFunctionOutput(outputBuffer);
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

        public static object CreateReturnValueFromFunctionOutput(IList<object> pipelineItems)
        {
            if (pipelineItems == null || pipelineItems.Count <= 0)
            {
                return null;
            }

            return pipelineItems.Count == 1 ? pipelineItems[0] : pipelineItems.ToArray();
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

        public void SetExternalInvoker(IExternalOrchestrationInvoker externalInvoker)
        {
            this.externalInvoker = externalInvoker;
        }
    }
}
