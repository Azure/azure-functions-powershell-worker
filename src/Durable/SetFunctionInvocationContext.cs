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
    /// Set the orchestration context.
    /// </summary>
    [Cmdlet("Set", "FunctionInvocationContext")]
    public class SetFunctionInvocationContextCommand : PSCmdlet
    {
        internal const string ContextKey = "OrchestrationContext";
        private const string StarterKey = "OrchestrationStarter";

        [Parameter(Mandatory = true, ParameterSetName = ContextKey)]
        public OrchestrationContext OrchestrationContext { get; set; }

        /// <summary>
        /// The orchestration client output binding name.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = StarterKey)]
        public string OrchestrationStarter { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "Clear")]
        public SwitchParameter Clear { get; set; }

        protected override void EndProcessing()
        {
            var privateData = (Hashtable)MyInvocation.MyCommand.Module.PrivateData;
            switch (ParameterSetName)
            {
                case ContextKey:
                    privateData[ContextKey] = OrchestrationContext;
                    break;

                case StarterKey:
                    privateData[StarterKey] = OrchestrationStarter;
                    break;

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
