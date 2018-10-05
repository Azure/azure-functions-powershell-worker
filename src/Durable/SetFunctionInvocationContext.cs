//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections;
using System.Management.Automation;

namespace Microsoft.Azure.Functions.PowerShellWorker.Commands
{
    /// <summary>
    /// Set the orchestration context.
    /// </summary>
    [Cmdlet("Set", "FunctionInvocationContext")]
    public class SetFunctionInvocationContextCommand : PSCmdlet
    {
        internal const string ContextKey = "OrchestrationContext";
        internal const string StarterKey = "OrchestrationStarter";

        /// <summary>
        /// Gets and sets the orchestration context.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ContextKey)]
        public OrchestrationContext OrchestrationContext { get; set; }

        /// <summary>
        /// Gets and sets the orchestration client output binding name.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = StarterKey)]
        public string OrchestrationStarter { get; set; }

        /// <summary>
        /// Gets and sets the orchestration context.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "Clear")]
        public SwitchParameter Clear { get; set; }

        /// <summary>
        /// EndProcessing
        /// </summary>
        protected override void EndProcessing()
        {
            var privateData = (Hashtable)this.MyInvocation.MyCommand.Module.PrivateData;
            switch (this.ParameterSetName)
            {
                case ContextKey:
                    privateData[ContextKey] = OrchestrationContext; break;
                case StarterKey:
                    privateData[StarterKey] = OrchestrationStarter; break;
                default:
                    if (Clear.IsPresent)
                    {
                        privateData.Remove(ContextKey);
                        privateData.Remove(StarterKey);
                    }
                    break;
            }
        }
    }
}
