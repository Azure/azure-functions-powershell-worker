//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    internal class DurableFunctionInfo
    {
        public DurableFunctionInfo(DurableFunctionType type, string orchestrationClientBindingName)
        {
            Type = type;
            OrchestrationClientBindingName = orchestrationClientBindingName;
        }

        public bool IsOrchestrationClient => OrchestrationClientBindingName != null;

        public bool IsOrchestrationFunction => Type == DurableFunctionType.OrchestrationFunction;

        public string OrchestrationClientBindingName { get; }

        public DurableFunctionType Type { get; }

        public bool ProvidesForcedDollarReturnValue => Type != DurableFunctionType.None;
    }
}
