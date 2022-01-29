//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.Azure.Functions.PowerShellWorker.Durable
{
    internal class OrchestrationBindingInfo
    {
        public OrchestrationBindingInfo(string parameterName, OrchestrationContext context)
        {
            ParameterName = parameterName;
            Context = context;
        }

        public string ParameterName { get; }

        public OrchestrationContext Context { get; }
    }
}
